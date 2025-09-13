namespace Email.Domain.IRepository;

public interface IFtpService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, Guid emailGuid);
    Task<bool> DeleteFileAsync(string filePath);
    Task<bool> FileExistsAsync(string filePath);
    Task<Stream> DownloadFileAsync(string filePath);
    //Task<string> GetCurrentTimeSlotDirectoryAsync();
    string GenerateDirectoryPath(Guid emailGuid, string fileName);
}