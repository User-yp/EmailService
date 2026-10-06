using Email.Domain;
using Email.Domain.Entity;

namespace Email.WebApp.Models;

/// <summary>
/// 邮件详情 API 模型。直接返回 EF 实体会因双向导航属性产生循环引用，因此统一投影。
/// </summary>
public class EmailDetailDto
{
    public Guid Id { get; init; }
    public List<string> To { get; init; } = [];
    public List<string>? Cc { get; init; }
    public List<string>? Bcc { get; init; }
    public string? From { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool IsHtml { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public EmailRecordDto? Record { get; init; }
    public List<AttachmentDto> Attachments { get; init; } = [];
}

public class EmailRecordDto
{
    public EmailStatus Status { get; init; }
    public int RetryCount { get; init; }
    public DateTime? LastRetryTime { get; init; }
    public DateTime? SentTime { get; init; }
    public DateTime? FailedTime { get; init; }
    public IReadOnlyList<string> FailedAddresses { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public string? ErrorDetails { get; init; }
}

public class AttachmentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public bool IsStoredInFtp { get; init; }
    public string? FilePath { get; init; }

    /// <summary>数据库里是否还保留二进制内容（归档到 FTP 后会清空）。</summary>
    public bool HasContent { get; init; }
}

public static class EmailDtoMapping
{
    public static EmailDetailDto ToDto(this EmailMessage message) => new()
    {
        Id = message.Id,
        To = message.To,
        Cc = message.Cc,
        Bcc = message.BCc,
        From = message.From,
        Subject = message.Subject,
        Body = message.Body,
        IsHtml = message.IsHtml,
        CreatedAt = message.CreatedAt,
        UpdatedAt = message.UpdatedAt,
        Record = message.Record?.ToDto(),
        Attachments = message.Attachments.Select(a => a.ToDto()).ToList()
    };

    public static EmailRecordDto ToDto(this EmailRecord record) => new()
    {
        Status = record.Status,
        RetryCount = record.RetryCount,
        LastRetryTime = record.LastRetryTime,
        SentTime = record.SentTime,
        FailedTime = record.FailedTime,
        FailedAddresses = record.FailedAdress ?? [],
        ErrorMessage = record.ErrorMessage,
        ErrorDetails = record.ErrorDetails
    };

    public static AttachmentDto ToDto(this Attachment attachment) => new()
    {
        Id = attachment.Id,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        FileSize = attachment.FileSize,
        IsStoredInFtp = attachment.IsStoredInFtp,
        FilePath = attachment.FilePath,
        HasContent = attachment.Content.Length > 0
    };
}
