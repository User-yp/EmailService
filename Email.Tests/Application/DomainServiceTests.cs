using Email.Domain;
using Email.Domain.Entity;
using Email.Extension.Option;
using Email.Infrastructure.Application;
using Email.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Email.Tests.Application;

/// <summary>
/// 验证 DomainService 的编排：入库 → 发送 → 标记状态 → 附件归档，以及重试与软删除。
/// </summary>
public class DomainServiceTests
{
    private readonly FakeEmailRepository _repository = new();
    private readonly FakeEmailHandler _handler = new();

    private DomainService CreateService(RetrySettings? settings = null)
        => new(_repository, _handler,
            // 默认冷却时间设为 0，便于单独验证"次数上限"这一维度
            settings ?? new RetrySettings { MaxRetryCount = 3, CooldownMinutes = 0 },
            NullLogger<DomainService>.Instance);

    private static EmailMessage NewMessage(List<Attachment>? attachments = null)
        => EmailMessage.Create(["to@example.com"], null, null, "from@example.com", "主题", "正文", attachments, false);

    private static List<Attachment> NewAttachments() => [Attachment.Create("a.txt", "text/plain", [1, 2, 3])];

    // ------------------------------------------------------------------ 发送

    [Fact]
    public async Task SendEmailAsync_OnSuccess_PersistsMarksSentAndArchivesAttachments()
    {
        var service = CreateService();

        var emailId = await service.SendEmailAsync(["to@example.com"], ["cc@example.com"], ["bcc@example.com"],
            "from@example.com", "主题", "正文", NewAttachments(), isHtml: true);

        var message = Assert.Single(_repository.Added);
        Assert.Equal(emailId, message.Id);
        Assert.True(message.IsHtml);
        Assert.Equal(new[] { "cc@example.com" }, message.Cc!);
        Assert.Single(message.Attachments);

        Assert.Equal(EmailStatus.Sent, message.Record.Status);
        Assert.Single(_repository.ArchivedToFtp); // 发送成功后才归档附件
        Assert.Single(_repository.Updated);       // finally 中落库状态
    }

    [Fact]
    public async Task SendEmailAsync_WithoutAttachments_SkipsFtpArchive()
    {
        var service = CreateService();

        await service.SendEmailAsync(["to@example.com"], null, null, null, "主题", "正文", null, false);

        Assert.Empty(_repository.ArchivedToFtp);
    }

    [Fact]
    public async Task SendEmailAsync_OnFailure_MarksFailedRethrowsAndStillPersists()
    {
        _handler.ResultFactory = _ => new InvalidOperationException("SMTP 不可用");
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendEmailAsync(["to@example.com"], null, null, null, "主题", "正文", null, false));

        Assert.Equal("SMTP 不可用", exception.Message);

        var message = Assert.Single(_repository.Added);
        Assert.Equal(EmailStatus.Failed, message.Record.Status);
        Assert.Equal("SMTP 不可用", message.Record.ErrorMessage);
        Assert.Single(_repository.Updated);       // 失败也必须落库，供后台重试调度发现
        Assert.Empty(_repository.ArchivedToFtp);  // 失败不归档附件
    }

    [Fact]
    public async Task SendEmailAsync_WhenFtpArchiveFails_StillReportsSuccess()
    {
        _repository.UploadFtpBehavior = _ => throw new IOException("FTP 不可用");
        var service = CreateService();

        var emailId = await service.SendEmailAsync(["to@example.com"], null, null, null, "主题", "正文",
            NewAttachments(), false);

        var message = Assert.Single(_repository.Added);
        Assert.Equal(emailId, message.Id);
        Assert.Equal(EmailStatus.Sent, message.Record.Status);
    }

    [Fact]
    public async Task SendEmailWithAttachmentsAsync_UsesTheSamePipeline()
    {
        var service = CreateService();

        var emailId = await service.SendEmailWithAttachmentsAsync("from@example.com", ["to@example.com"],
            "主题", "正文", NewAttachments());

        Assert.NotEqual(Guid.Empty, emailId);
        Assert.Equal(EmailStatus.Sent, Assert.Single(_repository.Added).Record.Status);
        Assert.Single(_repository.ArchivedToFtp);
    }

    // ------------------------------------------------------------------ 重试

    [Fact]
    public async Task RetryEmailAsync_ReturnsFalseWhenMessageMissing()
    {
        Assert.False(await CreateService().RetryEmailAsync(Guid.NewGuid()));
        Assert.Equal(0, _handler.SendCount);
    }

    [Fact]
    public async Task RetryEmailAsync_ReturnsFalseWhenMessageIsNotRetryable()
    {
        var message = NewMessage();
        message.MarkAsSent();
        _repository.Seed(message);

        Assert.False(await CreateService().RetryEmailAsync(message.Id));
        Assert.Equal(0, _handler.SendCount);
    }

    [Fact]
    public async Task RetryEmailAsync_ReturnsFalseWhenMaxRetryCountReached()
    {
        var message = NewMessage();
        message.MarkAsFailed("boom");
        message.MarkForRetry(["to@example.com"]);
        message.MarkForRetry(["to@example.com"]);
        _repository.Seed(message);
        Assert.Equal(2, message.Record.RetryCount);

        var service = CreateService(new RetrySettings { MaxRetryCount = 2, CooldownMinutes = 0 });

        Assert.False(await service.RetryEmailAsync(message.Id));
        Assert.Equal(0, _handler.SendCount);
    }

    [Fact]
    public async Task RetryEmailAsync_ReturnsFalseDuringCooldown()
    {
        var message = NewMessage();
        message.MarkAsFailed("boom");
        message.MarkForRetry(["to@example.com"]);
        _repository.Seed(message);

        var service = CreateService(new RetrySettings { MaxRetryCount = 3, CooldownMinutes = 5 });

        Assert.False(await service.RetryEmailAsync(message.Id));
        Assert.Equal(0, _handler.SendCount);
    }

    [Fact]
    public async Task RetryEmailAsync_OnSuccess_MarksSent()
    {
        var message = NewMessage();
        message.MarkAsFailed("boom");
        message.MarkForRetry(["to@example.com"]);
        _repository.Seed(message);

        Assert.True(await CreateService().RetryEmailAsync(message.Id));

        Assert.Equal(EmailStatus.Sent, message.Record.Status);
        Assert.Equal(1, _handler.SendCount);
        Assert.Single(_repository.Updated);
    }

    [Fact]
    public async Task RetryEmailAsync_OnFailure_IncrementsRetryCount()
    {
        var message = NewMessage();
        message.MarkAsFailed("boom");
        message.MarkForRetry(["to@example.com"]);
        _repository.Seed(message);

        _handler.ResultFactory = _ => new SmtpRejectedException("550 mailbox unavailable");

        Assert.False(await CreateService().RetryEmailAsync(message.Id));

        Assert.Equal(EmailStatus.Retry, message.Record.Status);
        Assert.Equal(2, message.Record.RetryCount);
        Assert.Equal("550 mailbox unavailable", message.Record.ErrorMessage);
        Assert.Single(_repository.Updated);
    }

    // ------------------------------------------------------------------ 软删除

    [Fact]
    public async Task DeleteEmailAsync_DelegatesToRepository()
    {
        var message = NewMessage([Attachment.Create("a.txt", "text/plain", [1])]);
        _repository.Seed(message);
        var service = CreateService();

        Assert.True(await service.DeleteEmailAsync(message.Id));
        Assert.True(message.IsDeleted);
        Assert.True(message.Record.IsDeleted);
        Assert.True(Assert.Single(message.Attachments).IsDeleted);

        Assert.False(await service.DeleteEmailAsync(Guid.NewGuid()));
    }

    /// <summary>模拟 SMTP 服务器拒收。</summary>
    private sealed class SmtpRejectedException(string message) : Exception(message);
}
