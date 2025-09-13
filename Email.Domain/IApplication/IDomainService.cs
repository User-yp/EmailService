using Email.Domain.Entity;

namespace Email.Domain.IApplication;

public interface IDomainService
{
    Task<Guid> SendEmailAsync(string from, List<string> to, string subject, string body, bool isHtml = false);
    Task<Guid> SendEmailWithAttachmentsAsync(string from, List<string> to, string subject, string body,
        List<Attachment> attachments, bool isHtml = false);
    Task<EmailMessage> GetEmailStatusAsync(Guid emailId);
    Task<Attachment> GetEmailAttachmentAsync(Guid attachmentId);
}