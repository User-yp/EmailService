using Email.Domain.Entity;
using Email.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Email.Infrastructure.Query;

/// <summary>
/// 邮件读模型的查询组合逻辑。独立成类是为了让"能否翻译成 SQL"可以被测试直接用
/// <c>ToQueryString()</c> 验证，而不必依赖真实数据库。
/// </summary>
public static class EmailQueryComposer
{
    /// <summary>应用筛选与排序（不含分页）。</summary>
    public static IQueryable<EmailMessage> ComposeFiltered(IQueryable<EmailMessage> source, EmailQueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = source.AsNoTracking();

        if (filter.Status is { } status)
            query = query.Where(e => e.Record.Status == status);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            // 主题模糊匹配；收件人按完整地址匹配。
            // 不用 To.Any(t => t.Contains(keyword)) 做收件人模糊匹配：那个写法只有 SQL Server 能翻译，
            // InMemory 提供程序会抛"无法翻译"，测试环境与离线开发会踩坑。
            query = query.Where(e => e.Subject.Contains(keyword) || e.To.Contains(keyword));
        }

        // 按 Id 兜底排序，保证翻页结果稳定
        return filter.Descending
            ? query.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            : query.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id);
    }

    /// <summary>应用筛选、排序、分页并投影为列表读模型（不加载正文与附件二进制）。</summary>
    public static IQueryable<EmailSummary> ComposeSearch(IQueryable<EmailMessage> source, EmailQueryFilter filter)
        => ComposeFiltered(source, filter)
            .Skip(filter.Skip)
            .Take(filter.NormalizedPageSize)
            .Select(e => new EmailSummary
            {
                Id = e.Id,
                To = e.To,
                Subject = e.Subject,
                IsHtml = e.IsHtml,
                Status = e.Record.Status,
                RetryCount = e.Record.RetryCount,
                SentTime = e.Record.SentTime,
                FailedTime = e.Record.FailedTime,
                ErrorMessage = e.Record.ErrorMessage,
                AttachmentCount = e.Attachments.Count,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            });

    /// <summary>按发送状态分组统计。</summary>
    public static IQueryable<EmailStatusCount> ComposeStatusCounts(IQueryable<EmailMessage> source)
        => source.AsNoTracking()
            .GroupBy(e => e.Record.Status)
            .Select(g => new EmailStatusCount { Status = g.Key, Count = g.Count() });
}
