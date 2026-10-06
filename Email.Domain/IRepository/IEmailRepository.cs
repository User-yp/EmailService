using Email.Domain.Entity;
using Email.Domain.Models;

namespace Email.Domain.IRepository;

public interface IEmailRepository
{
    Task<EmailMessage?> GetByIdAsync(Guid id);
    Task<EmailMessage?> GetByIdWithAttachmentsAsync(Guid id);
    Task<IEnumerable<EmailMessage>> GetAllAsync();

    /// <summary>
    /// 按创建时间倒序取最近若干封邮件（含附件与发送记录）。
    /// </summary>
    Task<IEnumerable<EmailMessage>> GetRecentAsync(int take, CancellationToken cancellationToken = default);

    Task AddAsync(EmailMessage emailMessage);
    Task UpdateAsync(EmailMessage emailMessage);
    Task<Attachment?> GetAttachmentByIdAsync(Guid attachmentId);
    Task UploadFtpAsync(EmailMessage emailMessage);

    /// <summary>
    /// 获取满足重试条件的发送记录（状态为 Failed/Retry 且未超过最大重试次数）。
    /// </summary>
    Task<IEnumerable<EmailRecord>> GetRetryableRecordsAsync(int maxRetryCount, int batchSize,
        CancellationToken cancellationToken = default);

    Task<EmailRecord?> GetRecordByMessageIdAsync(Guid emailMessageId);

    Task UpdateRecordAsync(EmailRecord emailRecord);

    /// <summary>
    /// 软删除邮件（聚合根，连同其发送记录与附件）。
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid emailId);

    /// <summary>
    /// 按条件分页查询邮件列表（投影为读模型，不加载正文与附件二进制）。
    /// </summary>
    Task<PagedResult<EmailSummary>> SearchAsync(EmailQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计各状态邮件数量、附件总数与最近一次发送时间。
    /// </summary>
    Task<EmailStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
