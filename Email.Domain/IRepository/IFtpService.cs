namespace Email.Domain.IRepository;

public interface IFtpService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, Guid emailGuid);
    Task<bool> DeleteFileAsync(string filePath);
    Task<bool> FileExistsAsync(string filePath);
    Task<Stream> DownloadFileAsync(string filePath);
    //Task<string> GetCurrentTimeSlotDirectoryAsync();
    string GenerateDirectoryPath(Guid emailGuid, string fileName);

    /// <summary>
    /// 连接探测，用于诊断 FTP 是否可用。
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// 列出目录下的文件名。
    /// </summary>
    Task<List<string>> ListFilesAsync(string directoryPath);
}
