namespace Email.Domain.Models;

/// <summary>
/// 邮件列表读模型：只包含列表需要的字段，不含正文与附件二进制，避免列表页拖出大字段。
/// </summary>
public class EmailSummary
{
    public Guid Id { get; set; }
    public List<string> To { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public bool IsHtml { get; set; }
    public EmailStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentTime { get; set; }
    public DateTime? FailedTime { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttachmentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string Recipients => To.Count == 0 ? "-" : string.Join(", ", To);
}
