namespace Email.WebApp.Models;

/// <summary>
/// 发送测试邮件的 multipart/form-data 表单。
/// </summary>
public class SendEmailForm
{
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string? From { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; }
    public List<IFormFile>? Files { get; set; }

    /// <summary>
    /// 按逗号、分号、空白与换行拆分地址并去重；返回 null 表示未填写。
    /// </summary>
    public static List<string>? SplitAddresses(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var addresses = value
            .Split([',', ';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return addresses.Count == 0 ? null : addresses;
    }
}
