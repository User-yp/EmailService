using Email.Domain.Entity;
using Email.Domain.IApplication;
using Email.Domain.IRepository;
using Email.Extension.Attributes;
using Email.Extension.Option;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Attachment = Email.Domain.Entity.Attachment;

namespace Email.Infrastructure.Application;
[Service(ServiceLifetime.Scoped)]
public class DomainService : IDomainService
{
    private readonly IEmailRepository _emailRepository;
    private readonly IEmailHandler _emailHandler;
    private readonly RetrySettings _retrySettings;
    private readonly ILogger<DomainService> _logger;

    public DomainService(IEmailRepository emailRepository, IEmailHandler emailHandler,
        RetrySettings retrySettings, ILogger<DomainService> logger)
    {
        _emailRepository = emailRepository;
        _emailHandler = emailHandler;
        _retrySettings = retrySettings;
        _logger = logger;
    }

    public async Task<Guid> SendEmailAsync(List<string> to, List<string>? cc, List<string>? bcc, string? from, string subject, string body, List<Attachment>? attachments, bool isHtml = false)
    {

        var emailMessage = EmailMessage.Create(to, cc, bcc, from, subject, body, attachments, isHtml);
        await _emailRepository.AddAsync(emailMessage);

        try
        {
            await _emailHandler.SendEmailAsync(emailMessage);
            emailMessage.MarkAsSent();
            // 发送成功后再把附件归档到 FTP，避免 FTP 写入拖慢发送链路
            await ArchiveAttachmentsAsync(emailMessage);
        }
        catch (Exception ex)
        {
            emailMessage.MarkAsFailed(ex.Message, ex.ToString());
            throw;
        }
        finally
        {
            await _emailRepository.UpdateAsync(emailMessage);
        }

        return emailMessage.Id;
    }

    public async Task<EmailMessage?> GetEmailStatusAsync(Guid emailId)
    {
        return await _emailRepository.GetByIdWithAttachmentsAsync(emailId);
    }

    public async Task<Attachment?> GetEmailAttachmentAsync(Guid attachmentId)
    {
        return await _emailRepository.GetAttachmentByIdAsync(attachmentId);
    }

    public Task<Guid> SendEmailWithAttachmentsAsync(string from, List<string> to, string subject, string body,
        List<Attachment> attachments, bool isHtml = false)
        => SendEmailAsync(to, null, null, from, subject, body, attachments, isHtml);

    /// <summary>
    /// 重试指定邮件：先由领域层校验次数与冷却时间，再执行发送。
    /// </summary>
    public async Task<bool> RetryEmailAsync(Guid emailId)
    {
        var emailMessage = await _emailRepository.GetByIdWithAttachmentsAsync(emailId);
        if (emailMessage == null)
        {
            _logger.LogWarning("重试跳过：未找到邮件 {EmailId}", emailId);
            return false;
        }

        var cooldownPeriod = TimeSpan.FromMinutes(_retrySettings.CooldownMinutes);
        if (!emailMessage.CanRetry(_retrySettings.MaxRetryCount, cooldownPeriod))
        {
            _logger.LogInformation("重试跳过：邮件 {EmailId} 不满足重试条件，当前状态 {Status}，已重试 {RetryCount} 次",
                emailId, emailMessage.Record.Status, emailMessage.Record.RetryCount);
            return false;
        }

        try
        {
            await _emailHandler.SendEmailAsync(emailMessage);
            emailMessage.MarkAsSent();
            await ArchiveAttachmentsAsync(emailMessage);
            _logger.LogInformation("邮件 {EmailId} 重试成功", emailId);
            return true;
        }
        catch (Exception ex)
        {
            emailMessage.MarkForRetry(emailMessage.To.ToList(), ex.Message, ex.ToString());
            _logger.LogWarning(ex, "邮件 {EmailId} 重试失败", emailId);
            return false;
        }
        finally
        {
            await _emailRepository.UpdateAsync(emailMessage);
        }
    }

    /// <summary>
    /// 软删除邮件（聚合根，连同发送记录与附件一起置位删除标记）。
    /// </summary>
    public async Task<bool> DeleteEmailAsync(Guid emailId)
    {
        var deleted = await _emailRepository.SoftDeleteAsync(emailId);
        if (deleted)
        {
            _logger.LogInformation("邮件 {EmailId} 已软删除", emailId);
        }

        return deleted;
    }

    private async Task ArchiveAttachmentsAsync(EmailMessage emailMessage)
    {
        if (emailMessage.Attachments.Count == 0)
            return;

        try
        {
            await _emailRepository.UploadFtpAsync(emailMessage);
        }
        catch (Exception ex)
        {
            // 归档失败不影响发送结果，附件内容保留在数据库中
            _logger.LogWarning(ex, "邮件 {EmailId} 的附件归档到 FTP 失败，附件内容保留在数据库中", emailMessage.Id);
        }
    }
}
