using Email.Domain.Entity;
using Email.Domain.IRepository;

namespace Email.Tests.Fakes;

/// <summary>
/// 可控的 SMTP 处理器替身：按配置成功或失败，并记录调用次数。
/// </summary>
internal sealed class FakeEmailHandler : IEmailHandler
{
    public List<EmailMessage> Sent { get; } = [];

    public int SendCount => Sent.Count;

    /// <summary>返回 null 表示发送成功，返回异常表示发送失败。</summary>
    public Func<EmailMessage, Exception?>? ResultFactory { get; set; }

    public Task SendEmailAsync(EmailMessage emailMessage)
    {
        Sent.Add(emailMessage);

        var exception = ResultFactory?.Invoke(emailMessage);
        return exception == null ? Task.CompletedTask : Task.FromException(exception);
    }
}
