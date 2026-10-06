using Email.Domain;
using Email.Domain.Entity;
using Xunit;

namespace Email.Tests.Domain;

public class EmailMessageTests
{
    private static EmailMessage NewMessage(List<Attachment>? attachments = null)
        => EmailMessage.Create(["to@example.com"], null, null, "from@example.com", "主题", "正文", attachments, false);

    [Theory]
    [InlineData(null, "正文")]
    [InlineData("", "正文")]
    [InlineData("主题", null)]
    [InlineData("主题", "")]
    public void Create_RejectsMissingSubjectOrBody(string? subject, string? body)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            EmailMessage.Create(["to@example.com"], null, null, null, subject!, body!, null, false));
    }

    [Fact]
    public void Create_RejectsEmptyRecipientList()
    {
        Assert.Throws<ArgumentException>(() =>
            EmailMessage.Create([], null, null, null, "主题", "正文", null, false));
    }

    [Fact]
    public void Create_InitializesRecordForTheMessage()
    {
        var message = NewMessage();

        Assert.Equal(EmailStatus.Init, message.Record.Status);
        Assert.Equal(message.Id, message.Record.EmailMessageId);
    }

    [Fact]
    public void Create_AttachesProvidedAttachments()
    {
        var attachment = Attachment.Create("a.txt", "text/plain", [1, 2, 3]);

        var message = NewMessage([attachment]);

        Assert.Same(attachment, Assert.Single(message.Attachments));
        Assert.Equal(message.Id, attachment.EmailMessageId);
    }

    [Fact]
    public void AddAttachment_AssociatesWithMessage()
    {
        var message = NewMessage();
        var attachment = Attachment.Create("a.txt", "text/plain", [1, 2, 3]);

        message.AddAttachment(attachment);

        Assert.Same(attachment, Assert.Single(message.Attachments));
        Assert.Equal(message.Id, attachment.EmailMessageId);
    }

    [Fact]
    public void AddAttachments_IgnoresNullAndEmptyList()
    {
        var message = NewMessage();

        message.AddAttachments(null);
        message.AddAttachments([]);

        Assert.Empty(message.Attachments);
    }

    [Fact]
    public void MarkAsSent_DelegatesToRecordAndKeepsDatabaseContent()
    {
        var attachment = Attachment.Create("a.txt", "text/plain", [1, 2, 3]);
        var message = NewMessage([attachment]);

        message.MarkAsSent();

        Assert.Equal(EmailStatus.Sent, message.Record.Status);
        // 未归档到 FTP 的附件，内容继续保留在数据库里
        Assert.NotEmpty(attachment.Content);
    }

    [Fact]
    public void MarkAsFailed_And_MarkForRetry_DelegateToRecord()
    {
        var message = NewMessage();

        message.MarkAsFailed("boom", "detail");
        Assert.Equal(EmailStatus.Failed, message.Record.Status);
        Assert.Equal("boom", message.Record.ErrorMessage);

        message.MarkForRetry(["to@example.com"], "retry error");
        Assert.Equal(EmailStatus.Retry, message.Record.Status);
        Assert.Equal(1, message.Record.RetryCount);
    }

    [Fact]
    public void CanRetry_DelegatesToRecord()
    {
        var message = NewMessage();
        Assert.False(message.CanRetry());

        message.MarkAsFailed("boom");

        Assert.True(message.CanRetry(maxRetryCount: 3, cooldownPeriod: TimeSpan.Zero));
    }

    [Fact]
    public void MarkAsDeleted_CascadesToRecordAndAttachments()
    {
        var attachment = Attachment.Create("a.txt", "text/plain", [1]);
        var message = NewMessage([attachment]);

        message.MarkAsDeleted();

        Assert.True(message.IsDeleted);
        Assert.True(message.Record.IsDeleted);
        Assert.True(attachment.IsDeleted);
    }

    [Fact]
    public void Restore_CascadesToRecordAndAttachments()
    {
        var attachment = Attachment.Create("a.txt", "text/plain", [1]);
        var message = NewMessage([attachment]);
        message.MarkAsDeleted();

        message.Restore();

        Assert.False(message.IsDeleted);
        Assert.False(message.Record.IsDeleted);
        Assert.False(attachment.IsDeleted);
    }
}
