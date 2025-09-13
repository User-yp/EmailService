
namespace Email.Domain.Entity;

public partial class Attachment
{
    public void AssociateWithEmail(Guid emailMessageId)
    {
        EmailMessageId = emailMessageId;
    }
    public static Attachment Create(string fileName, string contentType, byte[] content)
    {
        return new Attachment(fileName, contentType, content, content.Length / 1024);
    }

    public void ClearContent()
    {
        Content = Array.Empty<byte>();
    }
    // 在Attachment类中添加以下方法
    public void UpdateFtpInfo(string ftpFilePath)
    {
        FilePath = ftpFilePath;
        IsStoredInFtp = !string.IsNullOrEmpty(ftpFilePath);

        if (IsStoredInFtp)
        {
            Content = Array.Empty<byte>();
        }
    }
}
