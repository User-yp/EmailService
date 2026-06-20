using Email.Domain.Entity;
using Email.Domain.IRepository;
using Email.Extension.Attributes;
using Email.Extension.Option;
using Email.Infrastructure.Factory;
using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Email.Infrastructure.Repository;

[Service(ServiceLifetime.Scoped)]
public class EmailHandler : IEmailHandler
{
    private readonly SmtpSettings _smtpSettings;
    private readonly SmtpClientFactory _smtpClientFactory;
    private readonly IFtpService _ftpService;
    private readonly ILogger<EmailHandler> _logger;

    public EmailHandler(
        SmtpSettings smtpSettings,
        SmtpClientFactory clientFactory,
        IFtpService ftpService,
        ILogger<EmailHandler> logger)
    {
        _smtpSettings = smtpSettings;
        _smtpClientFactory = clientFactory;
        _ftpService = ftpService;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage emailMessage)
    {
        await SendWithRetryAsync(emailMessage, maxRetries: 1);
    }

    /// <summary>
    /// 带重试的邮件发送，处理连接断开和发送失败
    /// </summary>
    private async Task SendWithRetryAsync(EmailMessage emailMessage, int maxRetries)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var message = await BuildMimeMessageAsync(emailMessage);
                var client = await _smtpClientFactory.GetConnectedClientAsync();

                if (client is not { IsConnected: true })
                {
                    throw new SmtpConnectionException("SMTP client is not connected");
                }

                await client.SendAsync(message);

                _logger.LogInformation("Email sent successfully to {Recipients}",
                    string.Join(", ", emailMessage.To));
                return; // 发送成功，退出
            }
            catch (SmtpConnectionException) when (attempt < maxRetries)
            {
                // 连接问题，重置连接后重试
                _logger.LogWarning("SMTP connection issue on attempt {Attempt}, resetting and retrying",
                    attempt + 1);
                await _smtpClientFactory.ResetConnectionAsync();
            }
            catch (SmtpCommandException ex) when (attempt < maxRetries)
            {
                // MailKit SMTP 命令异常（含断开连接）
                _logger.LogWarning(ex, "SMTP command error on attempt {Attempt}, resetting and retrying",
                    attempt + 1);
                await _smtpClientFactory.ResetConnectionAsync();
            }
            catch (IOException ex) when (attempt < maxRetries)
            {
                // 底层网络断开
                _logger.LogWarning(ex, "Network I/O error on attempt {Attempt}, resetting and retrying",
                    attempt + 1);
                await _smtpClientFactory.ResetConnectionAsync();
            }
            catch (Exception) when (attempt >= maxRetries)
            {
                // 已达最大重试次数，向上抛出
                _logger.LogError("Failed to send email to {Recipients} after {Attempts} attempts",
                    string.Join(", ", emailMessage.To), attempt + 1);
                throw;
            }
        }
    }

    public async Task<MimeMessage> BuildMimeMessageAsync(EmailMessage emailMessage)
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
            byte[] content;

            if (attachment.IsStoredInFtp && attachment.Content.Length == 0)
            {
                // 附件已存储在 FTP，需要先下载
                _logger.LogInformation("Downloading attachment {FileName} from FTP", attachment.FileName);
                try
                {
                    await using var ftpStream = await _ftpService.DownloadFileAsync(attachment.FilePath!);
                    using var memoryStream = new MemoryStream();
                    await ftpStream.CopyToAsync(memoryStream);
                    content = memoryStream.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download attachment {FileName} from FTP, skipping",
                        attachment.FileName);
                    continue;
                }
            }
            else
            {
                content = attachment.Content;
            }

            using var stream = new MemoryStream(content);
            bodyBuilder.Attachments.Add(attachment.FileName, stream, ContentType.Parse(attachment.ContentType));
        }

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }
}
