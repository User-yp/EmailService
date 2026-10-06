using Email.Domain.Models;

namespace Email.WebApp.Models;

/// <summary>
/// 概览页数据：状态统计 + 最近邮件。
/// </summary>
public class DashboardDto
{
    public EmailStatistics Statistics { get; init; } = new();

    public IReadOnlyList<EmailSummary> RecentEmails { get; init; } = [];
}
