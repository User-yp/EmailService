using System.Net.Mail;

namespace Email.Domain.Entity;

public class Attachment : IAggregateRoot
{
    public Guid EmailMessageId { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public long FileSize { get; private set; }
    public byte[] Content { get; private set; }
    // 文件存储路径（本地路径或 FTP 链接）
    public string? FilePath { get; private set; }
    public EmailMessage EmailMessage { get; private set; }
    public bool IsStoredInFtp { get; private set; }

    // 构造函数（私有，确保通过工厂方法创建）
    private Attachment() { }

    // 工厂方法：创建附件
    private Attachment(string fileName, string contentType, byte[] content, long fileSize)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        FileSize = fileSize;
        IsStoredInFtp = false;
    }
   
}
