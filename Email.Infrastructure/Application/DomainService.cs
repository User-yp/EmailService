using Email.Domain.Entity;
using Email.Domain.IRepository;
using Email.Extension.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Attachment = Email.Domain.Entity.Attachment;

namespace Email.Infrastructure.Application;
[Service(ServiceLifetime.Scoped)]
public class DomainService
{
    private readonly IEmailRepository _emailRepository;
    private readonly IEmailHandler _emailHandler;

    public DomainService(IEmailRepository emailRepository, IEmailHandler emailHandler)
    {
        _emailRepository = emailRepository;
        _emailHandler = emailHandler;
    }

    public async Task<Guid> SendEmailAsync(List<string> to, List<string>? cc, List<string>? bcc, string? from, string subject, string body, List<Attachment>? attachments, bool isHtml = false)
    {

        var emailMessage = EmailMessage.Create(to, cc, bcc, from, subject, body, attachments, isHtml);
        //await _emailRepository.UploadFtpAsync(emailMessage);
        await _emailRepository.AddAsync(emailMessage);

        try
        {
            await _emailHandler.SendEmailAsync(emailMessage);
            emailMessage.MarkAsSent();
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

    public async Task<EmailMessage> GetEmailStatusAsync(Guid emailId)
    {
        return await _emailRepository.GetByIdWithAttachmentsAsync(emailId);
    }

    public async Task<Attachment> GetEmailAttachmentAsync(Guid attachmentId)
    {
        return await _emailRepository.GetAttachmentByIdAsync(attachmentId);
    }
}