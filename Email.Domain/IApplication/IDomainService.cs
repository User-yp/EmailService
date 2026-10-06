using Email.Domain.Entity;

namespace Email.Domain.IApplication;

public interface IDomainService
{
    /// <summary>
    /// 发送邮件（支持抄送、密送与多附件）。
    /// </summary>
    Task<Guid> SendEmailAsync(List<string> to, List<string>? cc, List<string>? bcc, string? from,
        string subject, string body, List<Attachment>? attachments, bool isHtml = false);

    /// <summary>
    /// 发送带附件的邮件（便捷重载）。
    /// </summary>
    Task<Guid> SendEmailWithAttachmentsAsync(string from, List<string> to, string subject, string body,
        List<Attachment> attachments, bool isHtml = false);

    Task<EmailMessage?> GetEmailStatusAsync(Guid emailId);

    Task<Attachment?> GetEmailAttachmentAsync(Guid attachmentId);

    /// <summary>
    /// 手动重试指定邮件。受最大重试次数与冷却时间约束，返回本次是否重试成功。
    /// </summary>
    Task<bool> RetryEmailAsync(Guid emailId);

    /// <summary>
    /// 软删除指定邮件（连同其附件与发送记录），数据物理保留。
    /// </summary>
    Task<bool> DeleteEmailAsync(Guid emailId);
}
