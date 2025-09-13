using Email.Domain.Entity;
using Email.Domain.IRepository;
using Email.Extension.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Mail;
using Attachment = Email.Domain.Entity.Attachment;

namespace Email.Infrastructure.Application;
[Service(ServiceLifetime.Scoped)]
public class DomainService //: IEmailService
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
        catch
        {
            emailMessage.MarkAsFailed();
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
    /*private async Task UploadAttachmentsToFtpAsync(EmailMessage emailMessage)
    {
        foreach (var attachment in emailMessage.Attachments.Where(a => !a.IsStoredInFtp))
        {
            try
            {
                using var memoryStream = new MemoryStream(attachment.Content);
                var ftpFilePath = await _ftpService.UploadFileAsync(memoryStream, attachment.FileName, emailMessage.Id);

                // 更新附件的FTP信息
                attachment.UpdateFtpInfo(ftpFilePath);
            }
            catch (Exception ex)
            {
            }
        }
    }*/
}