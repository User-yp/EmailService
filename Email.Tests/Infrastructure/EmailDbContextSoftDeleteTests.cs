using Email.Domain.Entity;
using Email.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Email.Tests.Infrastructure;

/// <summary>
/// 验证 EmailDbContext 把物理删除改写为软删除，以及附件字段的落库映射。
/// 使用 InMemory 提供程序在进程内运行，不依赖 SQL Server。
/// </summary>
public class EmailDbContextSoftDeleteTests
{
    private static EmailDbContext CreateContext(string databaseName)
        => new(new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static EmailMessage NewMessage(List<Attachment>? attachments = null)
        => EmailMessage.Create(["to@example.com"], null, null, "from@example.com", "主题", "正文", attachments, false);

    [Fact]
    public void Remove_IsRewrittenAsSoftDeleteAndRowSurvives()
    {
        var databaseName = Guid.NewGuid().ToString();
        var message = NewMessage();

        using (var context = CreateContext(databaseName))
        {
            context.EmailMessages.Add(message);
            context.SaveChanges();

            context.EmailMessages.Remove(message);
            context.SaveChanges();
        }

        using var verify = CreateContext(databaseName);
        var stored = verify.EmailMessages.IgnoreQueryFilters().Single(e => e.Id == message.Id);

        Assert.True(stored.IsDeleted);
        Assert.Equal(1, verify.EmailMessages.IgnoreQueryFilters().Count());
    }

    [Fact]
    public void SoftDeletedMessage_IsHiddenByGlobalQueryFilter()
    {
        var databaseName = Guid.NewGuid().ToString();
        var message = NewMessage();

        using (var context = CreateContext(databaseName))
        {
            context.EmailMessages.Add(message);
            context.SaveChanges();
            context.EmailMessages.Remove(message);
            context.SaveChanges();
        }

        using var verify = CreateContext(databaseName);
        Assert.Empty(verify.EmailMessages.ToList());
    }

    [Fact]
    public void MarkAsDeleted_PropagatesToRecordAndAttachment()
    {
        var databaseName = Guid.NewGuid().ToString();
        var message = NewMessage([Attachment.Create("a.txt", "text/plain", [1, 2, 3])]);

        using (var context = CreateContext(databaseName))
        {
            context.EmailMessages.Add(message);
            context.SaveChanges();

            message.MarkAsDeleted();
            context.SaveChanges();
        }

        using var verify = CreateContext(databaseName);
        Assert.True(verify.EmailMessages.IgnoreQueryFilters().Single().IsDeleted);
        Assert.True(verify.EmailRecords.IgnoreQueryFilters().Single().IsDeleted);
        Assert.True(verify.EmailAttachments.IgnoreQueryFilters().Single().IsDeleted);
    }

    [Fact]
    public void AttachmentFtpArchive_IsPersisted()
    {
        var databaseName = Guid.NewGuid().ToString();
        var attachment = Attachment.Create("a.txt", "text/plain", [1, 2, 3]);
        var message = NewMessage([attachment]);

        // 模拟"发送成功后归档到 FTP"：写入远程路径并清空数据库内容
        attachment.UpdateFtpInfo("25010112/0f7b/a.txt");

        using (var context = CreateContext(databaseName))
        {
            context.EmailMessages.Add(message);
            context.SaveChanges();
        }

        using var verify = CreateContext(databaseName);
        var stored = verify.EmailAttachments.Single();

        Assert.True(stored.IsStoredInFtp);
        Assert.Equal("25010112/0f7b/a.txt", stored.FilePath);
        Assert.Empty(stored.Content);
        Assert.Equal(3, stored.FileSize);
    }
}
