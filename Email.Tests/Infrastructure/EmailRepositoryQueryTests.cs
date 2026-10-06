using Email.Domain;
using Email.Domain.Entity;
using Email.Domain.Models;
using Email.Extension.Option;
using Email.Infrastructure;
using Email.Infrastructure.Repository;
using Email.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Email.Tests.Infrastructure;

/// <summary>
/// 仓储分页查询与统计行为（EF InMemory，不依赖 SQL Server）。
/// </summary>
public class EmailRepositoryQueryTests
{
    private static DateTime Now => new(2026, 1, 10, 12, 0, 0);

    private static (EmailRepository Repository, EmailDbContext Context) CreateRepository()
    {
        var context = new EmailDbContext(new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var repository = new EmailRepository(
            context,
            new FakeFtpService(),
            new FtpSettings { Enabled = false },
            NullLogger<EmailRepository>.Instance);

        return (repository, context);
    }

    private static void AddMessage(EmailDbContext context, string subject, string recipient,
        EmailStatus status, DateTime createdAt, int attachmentCount = 0)
    {
        var attachments = Enumerable.Range(0, attachmentCount)
            .Select(i => Attachment.Create($"file-{i}.txt", "text/plain", [1, 2, 3]))
            .ToList();

        var message = EmailMessage.Create([recipient], null, null, "from@example.com", subject, "body", attachments, false);

        switch (status)
        {
            case EmailStatus.Sent:
                message.MarkAsSent();
                break;
            case EmailStatus.Failed:
                message.MarkAsFailed("boom", "detail");
                break;
            case EmailStatus.Retry:
                message.MarkAsFailed("boom");
                message.MarkForRetry([recipient]);
                break;
        }

        context.EmailMessages.Add(message);
        context.SaveChanges();

        // 创建时间受聚合根保护，测试里通过 EF 的当前值访问器写入，以便验证排序
        context.Entry(message).Property(nameof(AggregateRoot.CreatedAt)).CurrentValue = createdAt;
        context.SaveChanges();
    }

    [Fact]
    public async Task SearchAsync_PagesAndOrdersByCreatedAtDescending()
    {
        var (repository, context) = CreateRepository();
        AddMessage(context, "最早", "a@example.com", EmailStatus.Sent, Now.AddDays(-3));
        AddMessage(context, "居中", "b@example.com", EmailStatus.Sent, Now.AddDays(-2));
        AddMessage(context, "最新", "c@example.com", EmailStatus.Sent, Now.AddDays(-1));

        var page = await repository.SearchAsync(new EmailQueryFilter { Page = 1, PageSize = 2 });

        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalPages);
        Assert.True(page.HasNext);
        Assert.False(page.HasPrevious);
        Assert.Equal("最新", page.Items[0].Subject);
        Assert.Equal("居中", page.Items[1].Subject);
    }

    [Fact]
    public async Task SearchAsync_ReturnsSecondPage()
    {
        var (repository, context) = CreateRepository();
        AddMessage(context, "最早", "a@example.com", EmailStatus.Sent, Now.AddDays(-3));
        AddMessage(context, "居中", "b@example.com", EmailStatus.Sent, Now.AddDays(-2));
        AddMessage(context, "最新", "c@example.com", EmailStatus.Sent, Now.AddDays(-1));

        var page = await repository.SearchAsync(new EmailQueryFilter { Page = 2, PageSize = 2 });

        Assert.Equal("最早", Assert.Single(page.Items).Subject);
        Assert.True(page.HasPrevious);
        Assert.False(page.HasNext);
    }

    [Fact]
    public async Task SearchAsync_FiltersByStatus()
    {
        var (repository, context) = CreateRepository();
        AddMessage(context, "成功", "a@example.com", EmailStatus.Sent, Now);
        AddMessage(context, "失败", "b@example.com", EmailStatus.Failed, Now);
        AddMessage(context, "重试", "c@example.com", EmailStatus.Retry, Now);

        var failed = await repository.SearchAsync(new EmailQueryFilter { Status = EmailStatus.Failed });

        var item = Assert.Single(failed.Items);
        Assert.Equal("失败", item.Subject);
        Assert.Equal(EmailStatus.Failed, item.Status);
        Assert.Equal(1, failed.Total);
    }

    [Fact]
    public async Task SearchAsync_KeywordMatchesSubjectFuzzyAndRecipientExactly()
    {
        var (repository, context) = CreateRepository();
        AddMessage(context, "月度账单", "alice@example.com", EmailStatus.Sent, Now);
        AddMessage(context, "登录验证码", "bob@example.com", EmailStatus.Sent, Now);

        var bySubject = await repository.SearchAsync(new EmailQueryFilter { Keyword = "账单" });
        var byRecipient = await repository.SearchAsync(new EmailQueryFilter { Keyword = "alice@example.com" });
        var byOtherDomain = await repository.SearchAsync(new EmailQueryFilter { Keyword = "@example.org" });

        Assert.Equal("月度账单", Assert.Single(bySubject.Items).Subject);
        // 收件人按完整地址匹配
        Assert.Equal("月度账单", Assert.Single(byRecipient.Items).Subject);
        Assert.Empty(byOtherDomain.Items);
    }

    [Fact]
    public async Task SearchAsync_ClampsPageSizeAndToleratesPagingBeyondRange()
    {
        var (repository, context) = CreateRepository();
        AddMessage(context, "只有一封", "a@example.com", EmailStatus.Sent, Now);

        var clamped = await repository.SearchAsync(new EmailQueryFilter { PageSize = 5000 });
        var beyond = await repository.SearchAsync(new EmailQueryFilter { Page = 99, PageSize = 10 });

        Assert.Equal(EmailQueryFilter.MaxPageSize, clamped.PageSize);
        Assert.Single(clamped.Items);
        Assert.Empty(beyond.Items);
        Assert.Equal(1, beyond.Total);
    }

    [Fact]
    public async Task SearchAsync_ProjectsAttachmentCount()
    {
        var (repository, context) = CreateRepository();
        AddMessage(context, "带附件", "a@example.com", EmailStatus.Sent, Now, attachmentCount: 2);

        var page = await repository.SearchAsync(new EmailQueryFilter());

        Assert.Equal(2, Assert.Single(page.Items).AttachmentCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_AggregatesStatusesAttachmentsAndLastSentTime()
    {
        var (repository, context) = CreateRepository();
        AddMessage(context, "成功1", "a@example.com", EmailStatus.Sent, Now.AddHours(-3));
        AddMessage(context, "成功2", "b@example.com", EmailStatus.Sent, Now.AddHours(-1), attachmentCount: 1);
        AddMessage(context, "失败", "c@example.com", EmailStatus.Failed, Now.AddHours(-2));
        AddMessage(context, "重试", "d@example.com", EmailStatus.Retry, Now.AddHours(-4));
        AddMessage(context, "待发送", "e@example.com", EmailStatus.Init, Now.AddHours(-5));

        var statistics = await repository.GetStatisticsAsync();

        Assert.Equal(5, statistics.Total);
        Assert.Equal(2, statistics.Sent);
        Assert.Equal(1, statistics.Failed);
        Assert.Equal(1, statistics.Retry);
        Assert.Equal(1, statistics.Init);
        Assert.Equal(1, statistics.AttachmentCount);
        Assert.NotNull(statistics.LastSentTime);
        // 成功率 = 已发送 /（已发送 + 失败 + 重试中）= 2 / 4
        Assert.Equal(50, statistics.SuccessRate);
    }

    [Fact]
    public async Task GetStatisticsAsync_OnEmptyDatabase_ReturnsZeroes()
    {
        var (repository, _) = CreateRepository();

        var statistics = await repository.GetStatisticsAsync();

        Assert.Equal(0, statistics.Total);
        Assert.Equal(0, statistics.SuccessRate);
        Assert.Null(statistics.LastSentTime);
    }
}
