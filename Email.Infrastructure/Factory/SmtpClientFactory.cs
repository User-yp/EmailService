using Email.Extension.Attributes;
using Email.Extension.Option;
using FluentFTP;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Email.Infrastructure.Factory;
[Service(ServiceLifetime.Singleton)]
public class SmtpClientFactory : IDisposable
{
    private SmtpClient? _smtpClient;
    private bool _disposed = false;
    private DateTime _lastActivity;
    private readonly SmtpSettings _smtpSettings;
    private readonly TimeSpan _inactivityTimeout;
    private readonly object _lockObject = new object();
    private readonly ILogger<SmtpClientFactory> _logger;
    private readonly CancellationTokenSource _monitorCts;
    private Task? _monitorTask;

    public SmtpClientFactory(SmtpSettings smtpSettings, ILogger<SmtpClientFactory> logger)
    {
        _logger = logger;
        _smtpSettings = smtpSettings;
        _lastActivity = DateTime.UtcNow;
        _inactivityTimeout = TimeSpan.FromMinutes(_smtpSettings.InactivityTimeout);
        _monitorCts = new CancellationTokenSource();

        // 启动后台监控任务
        _monitorTask = Task.Run(() => MonitorConnectionAsync(_monitorCts.Token));

        _logger.LogInformation("SmtpClientFactory initialized as singleton");
    }

    public async Task<SmtpClient> GetConnectedClientAsync()
    {
        ThrowIfDisposed();

        // 首先检查现有连接是否可用
        lock (_lockObject)
        {
            if (IsConnected && IsAuthenticated && _smtpClient != null)
            {
                _lastActivity = DateTime.Now;
                _logger.LogDebug("Reusing existing SMTP connection");
                return _smtpClient;
            }
        }

        // 创建新连接
        return await CreateAndConnectClientAsync();
    }

    public async Task<bool> TestConnectionAsync()
    {
        ThrowIfDisposed();

        try
        {
            using var testClient = new SmtpClient();
            await testClient.ConnectAsync(
                _smtpSettings.Server,
                _smtpSettings.Port,
                GetSocketOptions(),
                _monitorCts.Token);

            if (testClient.IsConnected)
            {
                _logger.LogInformation("SMTP connection test successful");
                await testClient.DisconnectAsync(true, _monitorCts.Token);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP connection test failed");
            return false;
        }
    }

    public async Task ReconnectAsync()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Manual reconnect requested");
        await CloseConnectionAsync();
        await CreateAndConnectClientAsync();
    }

    public async Task ResetConnectionAsync()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Resetting SMTP connection");
        await CloseConnectionAsync();
        await Task.Delay(TimeSpan.FromSeconds(1), _monitorCts.Token);
        await CreateAndConnectClientAsync();
    }

    private async Task MonitorConnectionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SMTP connection monitor started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);

                await CheckAndCleanupInactiveConnectionAsync();

                // 每5分钟测试一次连接健康度
                if (DateTime.Now.Minute % 5 == 0)
                {
                    await TestAndRecoverConnectionAsync();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SMTP connection monitor");
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        _logger.LogInformation("SMTP connection monitor stopped");
    }

    private async Task CheckAndCleanupInactiveConnectionAsync()
    {
        bool shouldCleanup = false;
        lock (_lockObject)
        {
            if (_smtpClient != null && (IsConnected || IsAuthenticated))
            {
                var inactivityTime = DateTime.Now - _lastActivity;
                shouldCleanup = inactivityTime > _inactivityTimeout;
            }
        }

        if (shouldCleanup)
        {
            _logger.LogInformation("Connection inactive for , will cleanup");
            await CloseConnectionAsync();
        }
    }

    private async Task TestAndRecoverConnectionAsync()
    {
        try
        {
            var status = false;
            lock (_lockObject)
            {
                status = IsConnected && IsAuthenticated;
            }

            if (status)
            {
                // 测试连接是否仍然健康
                var isHealthy = await TestConnectionAsync();
                if (!isHealthy)
                {
                    _logger.LogWarning("Connection test failed, attempting recovery");
                    await ResetConnectionAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection health");
        }
    }

    private async Task<SmtpClient> CreateAndConnectClientAsync()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Creating new SMTP connection");

        var client = new SmtpClient();
        try
        {
            client.Timeout = _smtpSettings.Timeout;

            await client.ConnectAsync(_smtpSettings.Server, _smtpSettings.Port, GetSocketOptions(), _monitorCts.Token);

            _logger.LogInformation("SMTP connected to {Server}:{Port}",
                _smtpSettings.Server, _smtpSettings.Port);

            // 认证
            if (!string.IsNullOrEmpty(_smtpSettings.Username) && !string.IsNullOrEmpty(_smtpSettings.Password))
            {
                await client.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password, _monitorCts.Token);
                _logger.LogInformation("SMTP authentication successful");
            }
            lock (_lockObject)
            {
                // 清理旧连接
                _smtpClient?.Dispose();
                _smtpClient = client;
                _lastActivity = DateTime.Now;
            }

            return client;
        }
        catch (Exception ex)
        {
            client.Dispose();
            _logger.LogError(ex, "Failed to create SMTP connection");
            throw new SmtpConnectionException("Failed to establish SMTP connection", ex);
        }
    }

    public async Task CloseConnectionAsync()
    {
        ThrowIfDisposed();

        SmtpClient? clientToDispose = null;
        lock (_lockObject)
        {
            if (_smtpClient == null) return;
            clientToDispose = _smtpClient;
            _smtpClient = null;
        }

        try
        {
            if (clientToDispose.IsConnected)
            {
                await clientToDispose.DisconnectAsync(true, _monitorCts.Token);
                _logger.LogInformation("SMTP connection closed gracefully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing SMTP connection");
        }
        finally
        {
            clientToDispose.Dispose();
            _lastActivity = DateTime.Now;
        }
    }

    private SecureSocketOptions GetSocketOptions() =>
        _smtpSettings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    public bool IsConnected
    {
        get
        {
            lock (_lockObject)
            {
                return _smtpClient != null && _smtpClient.IsConnected;
            }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (_lockObject)
            {
                return _smtpClient != null && _smtpClient.IsAuthenticated;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SmtpClientFactory));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Disposing SmtpClientFactory");

        // 停止监控任务
        _monitorCts.Cancel();
        try
        {
            _monitorTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex)
        {
            _logger.LogWarning(ex, "Error waiting for monitor task to complete");
        }

        // 清理资源
        _monitorCts.Dispose();

        // 关闭连接
        CloseConnectionAsync().GetAwaiter().GetResult();

        GC.SuppressFinalize(this);
    }

    ~SmtpClientFactory()
    {
        Dispose();
    }
}

public class SmtpConnectionException : Exception
{
    public SmtpConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
/*public class SmtpClientFactory 
{
    private SmtpClient? _smtpClient;
    private bool _disposed = false;
    private DateTime LastActivity;
    private readonly SmtpSettings _smtpSettings;
    private readonly int _monitorInterval;
    private readonly TimeSpan _inactivityTimeout;
    private readonly object _lockObject = new object();
    private readonly ILogger<SmtpClientFactory> _logger;
    private readonly CancellationTokenSource _monitorCts;
    private Task? _monitorTask;

    public SmtpClientFactory(SmtpSettings smtpSettings, ILogger<SmtpClientFactory> logger)
    {
        _logger = logger;
        _smtpSettings = smtpSettings;
        LastActivity = DateTime.Now;
        _monitorInterval = smtpSettings.MonitorInterval;
        _inactivityTimeout = TimeSpan.FromMinutes(smtpSettings.InactivityTimeout);
        _monitorCts = new CancellationTokenSource();
        // 启动后台监控任务
        _monitorTask = Task.Run(() => MonitorConnectionAsync(_monitorCts.Token));
    }

    private  async Task ExecuteAsync(CancellationToken stoppingToken=default)
    {
        _logger.LogInformation("SMTP Connection Monitor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorConnectionAsync();
                await Task.Delay(_monitorInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SMTP connection monitor");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        _logger.LogInformation("SMTP Connection Monitor stopped");
    }
    private async Task MonitorConnectionAsync()
    {
        if (_smtpClient == null)
            return;

        // 检查长时间未活动
        if (IsConnected||IsAuthenticated)
        {
            var inactivityTime = DateTime.Now - LastActivity;
            if (inactivityTime > _inactivityTimeout)
            {
                _logger.LogInformation("SMTP connection inactive for {InactivityTime}, closing", inactivityTime);
                await CloseConnectionAsync();
            }
        }
        //暂时不用，或者者延长时间
        //await TestConnAsync();
    }

    private async Task TestConnAsync()
    {
        var isAlive = await TestConnectionAsync();
        if (!isAlive)
        {
            _logger.LogWarning("SMTP connection test failed, connection may be stale");
            await ResetConnectionAsync();
        }
    }

    public async Task<SmtpClient> GetConnectedClientAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SmtpClientFactory));

        lock (_lockObject)
        {
            if (IsConnected&&IsAuthenticated && _smtpClient != null)
            {
                LastActivity = DateTime.Now;
                return _smtpClient;
            }
        }
        return await CreateAndConnectClientAsync();
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var testClient = new SmtpClient();
            await testClient.ConnectAsync(_smtpSettings.Server,_smtpSettings.Port,GetSocketOptions());

            if (testClient.IsConnected)
            {
                _logger.LogInformation( "SMTP connection test success");
                await testClient.DisconnectAsync(true);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP connection test failed");
            return false;
        }
    }

    public async Task ReconnectAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SmtpClientFactory));

        await CloseConnectionAsync();
        await CreateAndConnectClientAsync();
    }

    public async Task CloseConnectionAsync()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            if (_smtpClient == null) return;
        }

        try
        {
            if (_smtpClient.IsConnected)
            {
                await _smtpClient.DisconnectAsync(true);
                _logger.LogInformation("SMTP connection closed gracefully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while closing SMTP connection");
        }
        finally
        {
            lock (_lockObject)
            {
                _smtpClient?.Dispose();
                LastActivity = DateTime.Now;
            }
        }
    }

    public async Task ResetConnectionAsync()
    {
        await CloseConnectionAsync();
        await Task.Delay(1000); // 等待1秒后重连
        await CreateAndConnectClientAsync();
    }

    public bool IsConnected
    {
        get
        {
            lock (_lockObject)
            {
                return _smtpClient != null &&_smtpClient.IsConnected;
            }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (_lockObject)
            {
                return _smtpClient != null &&_smtpClient.IsAuthenticated;
            }
        }
    }

    private async Task<SmtpClient> CreateAndConnectClientAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SmtpClientFactory));

        var client = new SmtpClient();
        try
        {
            // 设置连接超时
            client.Timeout = _smtpSettings.Timeout;// _smtpSettings.Timeout;

            await client.ConnectAsync(_smtpSettings.Server,_smtpSettings.Port, GetSocketOptions());

            lock (_lockObject)
            {
                LastActivity = DateTime.Now;
            }

            _logger.LogInformation("SMTP connected to {Server}:{Port}",_smtpSettings.Server, _smtpSettings.Port);

            // 认证
            if (!string.IsNullOrEmpty(_smtpSettings.Username) &&!string.IsNullOrEmpty(_smtpSettings.Password))
            {
                await client.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password);
                _logger.LogInformation("SMTP authentication successful");
            }

            lock (_lockObject)
            {
                _smtpClient?.Dispose();
                _smtpClient = client;
                return _smtpClient;
            }
        }
        catch (Exception ex)
        {
            client.Dispose();
            _logger.LogError(ex, "Failed to create and connect SMTP client");
            throw new SmtpConnectionException("Failed to establish SMTP connection", ex);
        }
    }
    private SecureSocketOptions GetSocketOptions() =>
        _smtpSettings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            CloseConnectionAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal");
        }

        GC.SuppressFinalize(this);
    }

    ~SmtpClientFactory()
    {
        Dispose();
    }
    
}

public class SmtpConnectionException : Exception
{
    public SmtpConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}*/