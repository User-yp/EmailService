using Email.Domain;
using Email.Domain.Models;
using Email.Infrastructure;
using Email.Infrastructure.Query;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Email.Tests.Infrastructure;

/// <summary>
/// 读模型查询必须能下推到数据库执行。这里用 SqlServer 的 ToQueryString 生成 SQL 做校验，
/// 全程不建立真实连接，因此离线也能跑。
/// </summary>
public class EmailQueryComposerTests
{
    private static EmailDbContext CreateSqlServerContext() => new(
        new DbContextOptionsBuilder<EmailDbContext>()
            .UseSqlServer("Server=.;Database=emailserve;User Id=sa;Password=placeholder;TrustServerCertificate=true")
            .Options);

    [Fact]
    public void ComposeSearch_WithKeywordStatusAndPaging_TranslatesToSql()
    {
        using var context = CreateSqlServerContext();
        var filter = new EmailQueryFilter
        {
            Status = EmailStatus.Failed,
            Keyword = "alice",
            Page = 2,
            PageSize = 10
        };

        var sql = EmailQueryComposer.ComposeSearch(context.EmailMessages, filter).ToQueryString();

        Assert.False(string.IsNullOrWhiteSpace(sql));
        Assert.Contains("LIKE", sql);      // 主题关键字 → LIKE
        Assert.Contains("OPENJSON", sql);  // 收件人是 JSON 集合 → 必须下推为 OPENJSON 子查询
        Assert.Contains("OFFSET", sql);    // 分页 → OFFSET/FETCH
    }

    [Fact]
    public void ComposeFiltered_WithoutPaging_DoesNotEmitOffset()
    {
        using var context = CreateSqlServerContext();

        var sql = EmailQueryComposer.ComposeFiltered(context.EmailMessages, new EmailQueryFilter()).ToQueryString();

        // 统计总数用的查询不能带分页，否则会退化成"统计当前页"
        Assert.DoesNotContain("OFFSET", sql);
    }

    [Fact]
    public void ComposeStatusCounts_TranslatesToGroupBy()
    {
        using var context = CreateSqlServerContext();

        var sql = EmailQueryComposer.ComposeStatusCounts(context.EmailMessages).ToQueryString();

        Assert.Contains("GROUP BY", sql);
    }

    [Fact]
    public void ComposeSearch_ProjectsAttachmentCountAsSubQuery()
    {
        using var context = CreateSqlServerContext();

        var sql = EmailQueryComposer.ComposeSearch(context.EmailMessages, new EmailQueryFilter()).ToQueryString();

        // 附件数量用子查询统计，不能把附件实体（含二进制）拉回来
        Assert.Contains("AttachmentCount", sql);
        Assert.DoesNotContain("[a].[Content]", sql);
    }
}
