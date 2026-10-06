namespace Email.Domain.Models;

/// <summary>
/// 分组统计的中间读模型：某个状态下的邮件数量。
/// </summary>
public class EmailStatusCount
{
    public EmailStatus Status { get; set; }
    public int Count { get; set; }
}
