using Email.Domain.IRepository;

namespace Email.Tests.Fakes;

/// <summary>
/// FTP 服务替身：仓储/领域服务测试只关心"有没有调用"，默认不连任何服务器。
/// </summary>
internal sealed class FakeFtpService : IFtpService
{
    public List<string> UploadedFiles { get; } = [];
    public List<string> DeletedFiles { get; } = [];

    public Func<bool> ConnectionResult { get; set; } = () => true;

    public Task<string> UploadFileAsync(Stream fileStream, string fileName, Guid emailGuid)
    {
        var path = GenerateDirectoryPath(emailGuid, fileName);
        UploadedFiles.Add(path);
        return Task.FromResult(path);
    }

    public Task<bool> DeleteFileAsync(string filePath)
    {
        DeletedFiles.Add(filePath);
        return Task.FromResult(true);
    }

    public Task<bool> FileExistsAsync(string filePath) => Task.FromResult(true);

    public Task<Stream> DownloadFileAsync(string filePath)
        => Task.FromResult<Stream>(new MemoryStream("ftp-content"u8.ToArray()));

    public string GenerateDirectoryPath(Guid emailGuid, string fileName) => $"{emailGuid:N}/{fileName}";

    public Task<bool> TestConnectionAsync() => Task.FromResult(ConnectionResult());

    public Task<List<string>> ListFilesAsync(string directoryPath) => Task.FromResult(new List<string>());
}
