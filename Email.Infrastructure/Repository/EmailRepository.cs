using Email.Domain;
using Email.Domain.Entity;
using Email.Domain.Models;
using Email.Domain.IRepository;
using Email.Extension.Attributes;
using Email.Extension.Option;
using Email.Infrastructure;
using Email.Infrastructure.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Email.Infrastructure.Repository;

[Service(ServiceLifetime.Scoped)]
public class EmailRepository : IEmailRepository
{
    private readonly EmailDbContext _context;
    private readonly IFtpService _ftpService;
    private readonly FtpSettings _ftpSettings;
    private readonly ILogger<EmailRepository> _logger;

    public EmailRepository(EmailDbContext context, IFtpService ftpService, FtpSettings ftpSettings,
        ILogger<EmailRepository> logger)
    {
        _context = context;
        _ftpService = ftpService;
        _ftpSettings = ftpSettings;
        _logger = logger;
    }

    public async Task<EmailMessage?> GetByIdAsync(Guid id)
    {
        return await _context.EmailMessages
            .Include(e => e.Attachments)
            .Include(e => e.Record) // 包含记录
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<EmailMessage?> GetByIdWithAttachmentsAsync(Guid id)
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

    public async Task<IEnumerable<EmailMessage>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _context.EmailMessages
            .Include(e => e.Attachments)
            .Include(e => e.Record)
            .OrderByDescending(e => e.CreatedAt)
            // 服务端限制单次返回数量，避免全表加载
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<EmailSummary>> SearchAsync(EmailQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // 总数必须用未分页的查询统计，否则会退化成"只统计当前页"
        var total = await EmailQueryComposer.ComposeFiltered(_context.EmailMessages, filter)
            .CountAsync(cancellationToken);

        var items = new List<EmailSummary>();
        if (total > 0)
        {
            items = await EmailQueryComposer.ComposeSearch(_context.EmailMessages, filter)
                .ToListAsync(cancellationToken);
        }

        return new PagedResult<EmailSummary>
        {
            Items = items,
            Total = total,
            Page = filter.NormalizedPage,
            PageSize = filter.NormalizedPageSize
        };
    }

    public async Task<EmailStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var counts = await EmailQueryComposer.ComposeStatusCounts(_context.EmailMessages)
            .ToListAsync(cancellationToken);

        var statistics = new EmailStatistics
        {
            Total = counts.Sum(c => c.Count),
            AttachmentCount = await _context.EmailAttachments.CountAsync(cancellationToken),
            LastSentTime = await _context.EmailRecords
                .Where(r => r.SentTime != null)
                .OrderByDescending(r => r.SentTime)
                .Select(r => r.SentTime)
                .FirstOrDefaultAsync(cancellationToken)
        };

        foreach (var item in counts)
        {
            switch (item.Status)
            {
                case EmailStatus.Draft:
                    statistics.Draft = item.Count;
                    break;
                case EmailStatus.Init:
                    statistics.Init = item.Count;
                    break;
                case EmailStatus.Sent:
                    statistics.Sent = item.Count;
                    break;
                case EmailStatus.Failed:
                    statistics.Failed = item.Count;
                    break;
                case EmailStatus.Retry:
                    statistics.Retry = item.Count;
                    break;
            }
        }

        return statistics;
    }

    public async Task<IEnumerable<EmailRecord>> GetRetryableRecordsAsync(int maxRetryCount, int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.EmailRecords
            .Where(r => (r.Status == EmailStatus.Failed || r.Status == EmailStatus.Retry)
                        && r.RetryCount < maxRetryCount
                        && !r.EmailMessage.IsDeleted)
            .OrderBy(r => r.UpdatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
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

    public async Task<Attachment?> GetAttachmentByIdAsync(Guid attachmentId)
    {
        return await _context.EmailAttachments.FindAsync(attachmentId);
    }

    public async Task<EmailRecord?> GetRecordByMessageIdAsync(Guid emailMessageId)
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
        if (!_ftpSettings.Enabled)
        {
            _logger.LogDebug("FTP 附件归档已关闭，{Count} 个附件保留在数据库中", emailMessage.Attachments.Count);
            return;
        }

        foreach (var attachment in emailMessage.Attachments.Where(a => !a.IsStoredInFtp && a.Content.Length > 0))
        {
            try
            {
                using var memoryStream = new MemoryStream(attachment.Content);
                var ftpFilePath = await _ftpService.UploadFileAsync(memoryStream, attachment.FileName, emailMessage.Id);

                // 更新附件的FTP信息
                attachment.UpdateFtpInfo(ftpFilePath);
                _logger.LogInformation("附件 {FileName} 已归档到 FTP：{FtpPath}", attachment.FileName, ftpFilePath);
            }
            catch (Exception ex)
            {
                // FTP 上传失败不阻止邮件发送流程，记录日志后继续
                _logger.LogWarning(ex, "附件 {FileName} 归档到 FTP 失败，内容保留在数据库中", attachment.FileName);
            }
        }
    }

    public async Task<bool> SoftDeleteAsync(Guid emailId)
    {
        var emailMessage = await _context.EmailMessages
            .Include(e => e.Attachments)
            .Include(e => e.Record)
            .FirstOrDefaultAsync(e => e.Id == emailId);

        if (emailMessage == null)
            return false;

        // 聚合根统一置位删除标记，同时级联到发送记录与附件
        emailMessage.MarkAsDeleted();
        await _context.SaveChangesAsync();
        return true;
    }
}
