using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace Email.WebApi.Models;

/// <summary>
/// 发送邮件请求（JSON 请求体，无附件）。
/// </summary>
public class SendEmailRequest : IValidatableObject
{
    /// <summary>收件人列表，至少一个。</summary>
    [Required(ErrorMessage = "收件人不能为空")]
    public List<string> To { get; set; } = [];

    /// <summary>抄送。</summary>
    public List<string>? Cc { get; set; }

    /// <summary>密送。</summary>
    public List<string>? Bcc { get; set; }

    /// <summary>发件人；留空时使用 SmtpSettings.SenderEmail。</summary>
    public string? From { get; set; }

    [Required(ErrorMessage = "主题不能为空")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "正文不能为空")]
    public string Body { get; set; } = string.Empty;

    /// <summary>正文是否为 HTML。</summary>
    public bool IsHtml { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (To is not { Count: > 0 })
        {
            yield return new ValidationResult("至少需要一个收件人", [nameof(To)]);
        }

        foreach (var address in (To ?? []).Concat(Cc ?? []).Concat(Bcc ?? []))
        {
            if (string.IsNullOrWhiteSpace(address) || !MailAddress.TryCreate(address, out _))
                yield return new ValidationResult($"邮箱地址格式不正确：{address}", [nameof(To)]);
        }
    }
}

/// <summary>
/// 发送带附件邮件请求（multipart/form-data）。
/// </summary>
public class SendEmailFormRequest : SendEmailRequest
{
    /// <summary>附件列表，可传 0~N 个文件。</summary>
    public List<IFormFile>? Files { get; set; }
}
