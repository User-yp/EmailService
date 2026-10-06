using Email.Domain.Entity;

namespace Email.WebApi.Models;

/// <summary>
/// 邮件响应模型。直接返回 EF 实体会因为双向导航属性产生循环引用，
/// 因此统一投影成本模型。
/// </summary>
public class EmailMessageResponse
{
    public Guid Id { get; set; }
    public List<string> To { get; set; } = [];
    public List<string>? Cc { get; set; }
    public List<string>? Bcc { get; set; }
    public string? From { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public EmailRecordResponse? Record { get; set; }
    public List<AttachmentResponse> Attachments { get; set; } = [];
}

/// <summary>发送记录（状态、重试次数、失败原因）。</summary>
public class EmailRecordResponse
{
    public Guid EmailMessageId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime? LastRetryTime { get; set; }
    public DateTime? SentTime { get; set; }
    public DateTime? FailedTime { get; set; }
    public List<string>? FailedAddresses { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetails { get; set; }
}

/// <summary>附件元数据（不含二进制内容）。</summary>
public class AttachmentResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsStoredInFtp { get; set; }
    public string? FilePath { get; set; }

    /// <summary>数据库中是否还保留二进制内容（归档到 FTP 后会清空）。</summary>
    public bool HasContent { get; set; }
}

/// <summary>依赖组件连通性诊断结果。</summary>
public class DiagnosticsResponse
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Environment { get; set; } = string.Empty;
    public DiagnosticItem Smtp { get; set; } = new();
    public DiagnosticItem Ftp { get; set; } = new();
    public DiagnosticItem Database { get; set; } = new();
    public DiagnosticItem Redis { get; set; } = new();
}

public class DiagnosticItem
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public long ElapsedMilliseconds { get; set; }
}

public static class EmailResponseMapping
{
    public static EmailMessageResponse ToResponse(this EmailMessage message) => new()
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
        Record = message.Record?.ToResponse(),
        Attachments = message.Attachments.Select(a => a.ToResponse()).ToList()
    };

    public static EmailRecordResponse ToResponse(this EmailRecord record) => new()
    {
        EmailMessageId = record.EmailMessageId,
        Status = record.Status.ToString(),
        RetryCount = record.RetryCount,
        LastRetryTime = record.LastRetryTime,
        SentTime = record.SentTime,
        FailedTime = record.FailedTime,
        FailedAddresses = record.FailedAdress,
        ErrorMessage = record.ErrorMessage,
        ErrorDetails = record.ErrorDetails
    };

    public static AttachmentResponse ToResponse(this Attachment attachment) => new()
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
