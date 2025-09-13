namespace Email.Domain.Entity;

public partial class EmailMessage : IAggregateRoot
{
    public List<string> To { get; private set; }
    public List<string>? Cc { get; private set; }
    public List<string>? BCc { get; private set; }
    public string? From { get; private set; }
    public string Subject { get; private set; }
    public string Body { get; private set; }
    public bool IsHtml { get; private set; }

    // 导航属性
    public ICollection<Attachment> Attachments { get; private set; } = new List<Attachment>();
    public EmailRecord Record { get; private set; } // 一对一关系

    //提供无参构造方法给EFCore使用，并将其设为私有，防止在其他地方被调用
    private EmailMessage() { }

    public EmailMessage(List<string> to, List<string>? cc, List<string>? bcc, string? from, string subject, string body, List<Attachment>? attachments, bool isHtml = false)
    {
        To = to;
        Cc = cc;
        BCc = bcc;
        From = from;
        Subject = subject;
        Body = body;
        IsHtml = isHtml;

        // 添加附件
        AddAttachments(attachments);
        // 自动创建记录
        Record = new EmailRecord(Id);
    }
}