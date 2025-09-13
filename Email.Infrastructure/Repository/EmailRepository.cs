using Email.Domain;
using Email.Domain.Entity;
using Email.Domain.IRepository;
using Email.Extension.Attributes;
using Email.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Email.Infrastructure.Repository;

[Service(ServiceLifetime.Scoped)]
public class EmailRepository : IEmailRepository
{
    private readonly EmailDbContext _context;
    private readonly IFtpService ftpService;

    public EmailRepository(EmailDbContext context, IFtpService ftpService)
    {
        _context = context;
        this.ftpService = ftpService;
    }

    public async Task<EmailMessage> GetByIdAsync(Guid id)
    {
        return await _context.EmailMessages
            .Include(e => e.Attachments)
            .Include(e => e.Record) // 包含记录
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<EmailMessage> GetByIdWithAttachmentsAsync(Guid id)
    {
        return await _context.EmailMessages
            .Include(e => e.Attachments)
            .Include(e => e.Record) // 包含记录
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<EmailMessage>> GetAllAsync()
    {
        return await _context.EmailMessages
            .Include(e => e.Attachments)
            .Include(e => e.Record) // 包含记录
            .ToListAsync();
    }

    public async Task<IEnumerable<EmailRecord>> GetFailedRecordsAsync(int maxRetryCount = 3)
    {
        return await _context.EmailRecords
            .Where(r => r.Status == EmailStatus.Failed && r.RetryCount < maxRetryCount)
            .Include(r => r.EmailMessage)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmailRecord>> GetPendingRecordsAsync()
    {
        return await _context.EmailRecords
            .Where(r => r.Status == EmailStatus.Retry)
            .Include(r => r.EmailMessage)
            .ToListAsync();
    }

    public async Task AddAsync(EmailMessage emailMessage)
    {
        await _context.EmailMessages.AddAsync(emailMessage);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EmailMessage emailMessage)
    {
        _context.EmailMessages.Update(emailMessage);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateRecordAsync(EmailRecord emailRecord)
    {
        _context.EmailRecords.Update(emailRecord);
        await _context.SaveChangesAsync();
    }

    public async Task<Attachment> GetAttachmentByIdAsync(Guid attachmentId)
    {
        return await _context.EmailAttachments.FindAsync(attachmentId);
    }

    public async Task<EmailRecord> GetRecordByMessageIdAsync(Guid emailMessageId)
    {
        return await _context.EmailRecords
            .Include(r => r.EmailMessage)
            .FirstOrDefaultAsync(r => r.EmailMessageId == emailMessageId);
    }
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
    public async Task UploadFtpAsync(EmailMessage emailMessage)
    {
        foreach (var attachment in emailMessage.Attachments.Where(a => !a.IsStoredInFtp))
        {
            try
            {
                using var memoryStream = new MemoryStream(attachment.Content);
                var ftpFilePath = await ftpService.UploadFileAsync(memoryStream, attachment.FileName, emailMessage.Id);

                // 更新附件的FTP信息
                attachment.UpdateFtpInfo(ftpFilePath);
            }
            catch (Exception ex)
            {
            }
        }
    }
}