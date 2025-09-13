using Email.Domain.Entity;
using Email.Domain.IRepository;
using Email.Extension.Attributes;
using Email.Extension.Option;
using Email.Infrastructure.Factory;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Email.Infrastructure.Repository;
[Service(ServiceLifetime.Scoped)]
public class EmailHandler : IEmailHandler
{
    private readonly SmtpSettings _smtpSettings;
    private readonly SmtpClientFactory _smtpClientFactory;
    private readonly ILogger<EmailHandler> logger;

    public EmailHandler(SmtpSettings smtpSettings, SmtpClientFactory clientFactory, ILogger<EmailHandler> logger)
    {
        _smtpSettings = smtpSettings;
        this._smtpClientFactory = clientFactory;
        this.logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage emailMessage)
    {
        var message = await MimeMessage(emailMessage);
        SmtpClient client = null;

        try
        {
            // 使用连接工厂获取客户端
            client = await _smtpClientFactory.GetConnectedClientAsync();

            if (client == null || !client.IsConnected)
            {
                throw new SmtpConnectionException("SMTP client is not connected", null);
            }

            await client.SendAsync(message);

            logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", emailMessage.To));
        }
        catch (SmtpConnectionException)
        {
            // 连接问题，尝试重连一次
            logger.LogWarning("SMTP connection issue detected, attempting reconnect");
            await _smtpClientFactory.ResetConnectionAsync();

            // 重试
            client = await _smtpClientFactory.GetConnectedClientAsync();
            await client.SendAsync(message);

            logger.LogInformation("Email sent successfully after reconnect to {Recipients}",
                string.Join(", ", emailMessage.To));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", emailMessage.To));
            throw;
        }
    }
    public async Task<MimeMessage> MimeMessage(EmailMessage emailMessage)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtpSettings.SenderName, _smtpSettings.SenderEmail));
        message.To.AddRange(emailMessage.To.Select(e => new MailboxAddress("", e)));
        message.Subject = emailMessage.Subject;
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = emailMessage.IsHtml ? emailMessage.Body : null,
            TextBody = emailMessage.IsHtml ? null : emailMessage.Body
        };
        // 添加附件
        foreach (var attachment in emailMessage.Attachments)
        {
            using var stream = new MemoryStream(attachment.Content);
            bodyBuilder.Attachments.Add(attachment.FileName, stream, ContentType.Parse(attachment.ContentType));
        }
        message.Body = bodyBuilder.ToMessageBody();
        return await Task.FromResult(message);
    }
}
