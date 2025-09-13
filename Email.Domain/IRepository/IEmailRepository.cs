using Email.Domain.Entity;

namespace Email.Domain.IRepository;

public interface IEmailRepository
{
    Task<EmailMessage> GetByIdAsync(Guid id);
    Task<EmailMessage> GetByIdWithAttachmentsAsync(Guid id);
    Task<IEnumerable<EmailMessage>> GetAllAsync();
    Task AddAsync(EmailMessage emailMessage);
    Task UpdateAsync(EmailMessage emailMessage);
    Task<Attachment> GetAttachmentByIdAsync(Guid attachmentId);
    Task UploadFtpAsync(EmailMessage emailMessage);
}