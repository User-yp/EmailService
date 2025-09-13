using Email.Domain.IRepository;
using Email.Extension.Attributes;
using Email.Extension.Option;
using FluentFTP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Email.Infrastructure.Repository;

[Service(ServiceLifetime.Scoped)]
public class FtpService : IFtpService, IAsyncDisposable
{
    private readonly FtpSettings _ftpSettings;
    private readonly ILogger<FtpService> _logger;
    private readonly AsyncLazy<FtpClient> _ftpClient;

    public FtpService(FtpSettings ftpSettings, ILogger<FtpService> logger)
    {
        _ftpSettings = ftpSettings;
        _logger = logger;
        _ftpClient = new AsyncLazy<FtpClient>(CreateAndConnectFtpClientAsync);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, Guid emailGuid)
    {
        var directoryPath = GenerateDirectoryPath(emailGuid, fileName);
        var fullRemotePath = $"{directoryPath}";

        try
        {
            var client = await _ftpClient;

            // 确保目录存在
            await EnsureDirectoryExistsAsync(directoryPath);

            // 上传文件
            var success = client.UploadStream(fileStream, fullRemotePath);

            if (success == FtpStatus.Success)
            {
                _logger.LogInformation("File uploaded successfully: {FileName} to {Path}",
                    fileName, fullRemotePath);
                return fullRemotePath;
            }
            else
            {
                throw new Exception($"FTP upload failed for file: {fileName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName} to FTP", fileName);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            var client = await _ftpClient;

            if (client.FileExists(filePath))
            {
                client.DeleteFile(filePath);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file from FTP: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        try
        {
            var client = await _ftpClient;
            return client.FileExists(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<Stream> DownloadFileAsync(string filePath)
    {
        try
        {
            var client = await _ftpClient;

            if (!client.FileExists(filePath))
            {
                throw new FileNotFoundException($"File not found on FTP server: {filePath}");
            }

            var stream = new MemoryStream();
            var success = client.DownloadStream(stream, filePath);

            if (success)
            {
                stream.Position = 0;
                return stream;
            }
            else
            {
                throw new Exception($"Failed to download file: {filePath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from FTP: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> DirectoryExistsAsync(string path)
    {
        try
        {
            var client = await _ftpClient;
            return client.DirectoryExists(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking directory existence: {Path}", path);
            return false;
        }
    }

    public string GenerateDirectoryPath(Guid emailGuid, string fileName)
    {
        var now = DateTime.Now;
        var timeSlotDir = $"{now:yy}{now:MM}{now:dd}{now:HH}";
        return $"{timeSlotDir}/{emailGuid}/{fileName}";
    }

    private async Task EnsureDirectoryExistsAsync(string directoryPath)
    {
        try
        {
            var client = await _ftpClient;

            // 移除文件名部分，只保留目录路径
            var directoryOnlyPath = Path.GetDirectoryName(directoryPath);

            if (!string.IsNullOrEmpty(directoryOnlyPath))
            {
                // FluentFTP 会自动创建所有不存在的父目录
                client.CreateDirectory(directoryOnlyPath, true);
                _logger.LogInformation("Ensured directory exists: {Directory}", directoryOnlyPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure directory exists: {DirectoryPath}", directoryPath);
            throw;
        }
    }

    private async Task<FtpClient> CreateAndConnectFtpClientAsync()
    {
        var config = new FtpConfig
        {
            EncryptionMode = _ftpSettings.EnableSsl ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None,
            DataConnectionType = _ftpSettings.UsePassiveMode ? FtpDataConnectionType.AutoPassive : FtpDataConnectionType.AutoActive,
            SocketKeepAlive = false,
            ConnectTimeout = _ftpSettings.Timeout,
            DataConnectionConnectTimeout = _ftpSettings.Timeout,
            DataConnectionReadTimeout = _ftpSettings.Timeout,
            ReadTimeout = _ftpSettings.Timeout,
            //Encoding = Encoding.UTF8,
            ValidateAnyCertificate = true // 对于测试环境，可以跳过证书验证
        };
        var client = new FtpClient(_ftpSettings.Host, new NetworkCredential(_ftpSettings.Username, _ftpSettings.Password), _ftpSettings.Port, config);

        try
        {
            client.Connect();
            _logger.LogInformation("FTP client connected successfully to {Host}", _ftpSettings.Host);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to FTP server: {Host}", _ftpSettings.Host);
            client.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ftpClient != null)
        {
            var client = await _ftpClient;
            client.Dispose();
        }
    }
    // 可以添加这些方法到 IFtpService 接口
    public async Task<List<string>> ListFilesAsync(string directoryPath)
    {
        var client = await _ftpClient;
        var items = client.GetListing(directoryPath);
        return items.Where(x => x.Type == FtpObjectType.File).Select(x => x.Name).ToList();
    }

    public async Task<List<string>> ListDirectoriesAsync(string directoryPath)
    {
        var client = await _ftpClient;
        var items = client.GetListing(directoryPath);
        return items.Where(x => x.Type == FtpObjectType.Directory).Select(x => x.Name).ToList();
    }

    public async Task<long> GetFileSizeAsync(string filePath)
    {
        var client = await _ftpClient;
        return client.GetFileSize(filePath);
    }
}

// 辅助类：异步延迟初始化
public class AsyncLazy<T>
{
    private readonly Lazy<Task<T>> _lazyTask;

    public AsyncLazy(Func<T> valueFactory)
    {
        _lazyTask = new Lazy<Task<T>>(() => Task.Run(valueFactory));
    }

    public AsyncLazy(Func<Task<T>> taskFactory)
    {
        _lazyTask = new Lazy<Task<T>>(taskFactory);
    }

    public Task<T> Value => _lazyTask.Value;

    public TaskAwaiter<T> GetAwaiter() => _lazyTask.Value.GetAwaiter();
}