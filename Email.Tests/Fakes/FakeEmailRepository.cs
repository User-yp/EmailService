using Email.Domain;
using Email.Domain.Entity;
using Email.Domain.IRepository;

namespace Email.Tests.Fakes;

/// <summary>
/// 内存版邮件仓储替身：记录调用轨迹，用于验证领域服务的编排行为。
/// </summary>
internal sealed class FakeEmailRepository : IEmailRepository
{
    private readonly Dictionary<Guid, EmailMessage> _store = new();

    public List<EmailMessage> Added { get; } = [];
    public List<EmailMessage> Updated { get; } = [];
    public List<EmailMessage> ArchivedToFtp { get; } = [];

    /// <summary>注入 FTP 归档行为，用于模拟归档失败。</summary>
    public Func<EmailMessage, Task>? UploadFtpBehavior { get; set; }

    public void Seed(EmailMessage message) => _store[message.Id] = message;

    public Task AddAsync(EmailMessage emailMessage)
    {
        Added.Add(emailMessage);
        _store[emailMessage.Id] = emailMessage;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(EmailMessage emailMessage)
    {
        Updated.Add(emailMessage);
        _store[emailMessage.Id] = emailMessage;
        return Task.CompletedTask;
    }

    public Task UploadFtpAsync(EmailMessage emailMessage)
    {
        ArchivedToFtp.Add(emailMessage);
        return UploadFtpBehavior?.Invoke(emailMessage) ?? Task.CompletedTask;
    }

    public Task<EmailMessage?> GetByIdAsync(Guid id) => Task.FromResult(Find(id));

    public Task<EmailMessage?> GetByIdWithAttachmentsAsync(Guid id) => Task.FromResult(Find(id));

    public Task<IEnumerable<EmailMessage>> GetAllAsync()
        => Task.FromResult<IEnumerable<EmailMessage>>(_store.Values.ToList());

    public Task<IEnumerable<EmailMessage>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<EmailMessage>>(_store.Values.Take(take).ToList());

    public Task<Attachment?> GetAttachmentByIdAsync(Guid attachmentId)
        => Task.FromResult(_store.Values.SelectMany(m => m.Attachments).FirstOrDefault(a => a.Id == attachmentId));

    public Task<IEnumerable<EmailRecord>> GetRetryableRecordsAsync(int maxRetryCount, int batchSize,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<EmailRecord>>(_store.Values
            .Select(m => m.Record)
            .Where(r => (r.Status == EmailStatus.Failed || r.Status == EmailStatus.Retry) && r.RetryCount < maxRetryCount)
            .Take(batchSize)
            .ToList());

    public Task<EmailRecord?> GetRecordByMessageIdAsync(Guid emailMessageId)
        => Task.FromResult(Find(emailMessageId)?.Record);

    public Task UpdateRecordAsync(EmailRecord emailRecord) => Task.CompletedTask;

    public Task<bool> SoftDeleteAsync(Guid emailId)
    {
        var message = Find(emailId);
        if (message == null)
            return Task.FromResult(false);

        message.MarkAsDeleted();
        return Task.FromResult(true);
    }

    private EmailMessage? Find(Guid id) => _store.TryGetValue(id, out var message) ? message : null;
}
