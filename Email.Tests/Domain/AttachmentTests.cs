using Email.Domain.Entity;
using Xunit;

namespace Email.Tests.Domain;

public class AttachmentTests
{
    [Fact]
    public void Create_StoresFileSizeInBytes()
    {
        var content = new byte[2048];

        var attachment = Attachment.Create("a.bin", "application/octet-stream", content);

        Assert.Equal(2048, attachment.FileSize);
        Assert.False(attachment.IsStoredInFtp);
        Assert.Null(attachment.FilePath);
    }

    [Fact]
    public void Create_ThrowsWhenFileNameMissing()
    {
        Assert.Throws<ArgumentNullException>(() => Attachment.Create(null!, "text/plain", [1, 2, 3]));
    }

    [Fact]
    public void Create_ThrowsWhenContentTypeMissing()
    {
        Assert.Throws<ArgumentNullException>(() => Attachment.Create("a.txt", null!, [1, 2, 3]));
    }

    [Fact]
    public void UpdateFtpInfo_MarksArchivedAndDropsDatabaseContent()
    {
        var attachment = Attachment.Create("a.txt", "text/plain", [1, 2, 3]);

        attachment.UpdateFtpInfo("25010112/0f7b.../a.txt");

        Assert.True(attachment.IsStoredInFtp);
        Assert.Equal("25010112/0f7b.../a.txt", attachment.FilePath);
        Assert.Empty(attachment.Content);
    }

    [Fact]
    public void UpdateFtpInfo_WithEmptyPath_KeepsDatabaseContent()
    {
        var attachment = Attachment.Create("a.txt", "text/plain", [1, 2, 3]);

        attachment.UpdateFtpInfo(string.Empty);

        Assert.False(attachment.IsStoredInFtp);
        Assert.NotEmpty(attachment.Content);
    }

    [Fact]
    public void ClearContent_EmptiesContent()
    {
        var attachment = Attachment.Create("a.txt", "text/plain", [1, 2, 3]);

        attachment.ClearContent();

        Assert.Empty(attachment.Content);
    }

    [Fact]
    public void AssociateWithEmail_SetsMessageId()
    {
        var attachment = Attachment.Create("a.txt", "text/plain", [1]);
        var emailId = Guid.NewGuid();

        attachment.AssociateWithEmail(emailId);

        Assert.Equal(emailId, attachment.EmailMessageId);
    }
}
