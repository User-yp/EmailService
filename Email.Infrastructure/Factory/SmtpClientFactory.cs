using Email.Extension.Attributes;
using Email.Extension.Option;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Email.Infrastructure.Factory;

[Service(ServiceLifetime.Singleton)]
public class SmtpClientFactory : IDisposable, IAsyncDisposable
{
    private SmtpClient? _smtpClient;
    private bool _disposed = false;
    private DateTime _lastActivity;
    private DateTime _lastHealthCheck;
    private readonly SmtpSettings _smtpSettings;
    private readonly TimeSpan _inactivityTimeout;
    private readonly TimeSpan _monitorInterval;
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private readonly ILogger<SmtpClientFactory> _logger;
    private readonly CancellationTokenSource _monitorCts;
    private Task? _monitorTask;

    public SmtpClientFactory(SmtpSettings smtpSettings, ILogger<SmtpClientFactory> logger)
    {
        _logger = logger;
        _smtpSettings = smtpSettings;
        _lastActivity = DateTime.UtcNow;
        _lastHealthCheck = DateTime.UtcNow;
        _inactivityTimeout = TimeSpan.FromMinutes(_smtpSettings.InactivityTimeout);
        _monitorInterval = _smtpSettings.MonitorInterval > 0
            ? TimeSpan.FromSeconds(_smtpSettings.MonitorInterval)
            : TimeSpan.FromMinutes(1);
        _monitorCts = new CancellationTokenSource();

        // 启动后台监控任务
        _monitorTask = Task.Run(() => MonitorConnectionAsync(_monitorCts.Token));

        _logger.LogInformation("SmtpClientFactory initialized as singleton");
    }

    public async Task<SmtpClient> GetConnectedClientAsync()
    {
        ThrowIfDisposed();

        // 快速检查：现有连接是否可用
        var existingClient = GetExistingClient();
        if (existingClient != null)
        {
            _lastActivity = DateTime.UtcNow;
            _logger.LogDebug("Reusing existing SMTP connection");
            return existingClient;
        }

        // 需要创建新连接，使用信号量确保只有一个线程执行连接创建
        await _connectionSemaphore.WaitAsync(_monitorCts.Token);
        try
        {
            // 双重检查：等待信号量期间可能已有其他线程创建了连接
            var doubleCheckClient = GetExistingClient();
            if (doubleCheckClient != null)
            {
                _lastActivity = DateTime.UtcNow;
                return doubleCheckClient;
            }

            await CreateAndConnectClientAsync();
            return _smtpClient!;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// 获取当前可用的已连接客户端（线程安全）
    /// </summary>
    private SmtpClient? GetExistingClient()
    {
        lock (_connectionSemaphore)
        {
            if (_smtpClient is { IsConnected: true, IsAuthenticated: true })
            {
                return _smtpClient;
            }
            return null;
        }
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
        await _connectionSemaphore.WaitAsync(_monitorCts.Token);
        try
        {
            await CreateAndConnectClientAsync();
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public async Task ResetConnectionAsync()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Resetting SMTP connection");
        await CloseConnectionAsync();
        await Task.Delay(TimeSpan.FromSeconds(1), _monitorCts.Token);
        await _connectionSemaphore.WaitAsync(_monitorCts.Token);
        try
        {
            await CreateAndConnectClientAsync();
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private async Task MonitorConnectionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SMTP connection monitor started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_monitorInterval, cancellationToken);

                await CheckAndCleanupInactiveConnectionAsync();

                // 按健康检查间隔测试连接
                if (DateTime.UtcNow - _lastHealthCheck >= _healthCheckInterval)
                {
                    await TestAndRecoverConnectionAsync();
                    _lastHealthCheck = DateTime.UtcNow;
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
        lock (_connectionSemaphore)
        {
            if (_smtpClient != null && (_smtpClient.IsConnected || _smtpClient.IsAuthenticated))
            {
                var inactivityTime = DateTime.UtcNow - _lastActivity;
                shouldCleanup = inactivityTime > _inactivityTimeout;
            }
        }

        if (shouldCleanup)
        {
            _logger.LogInformation("Connection inactive for over {Timeout}min, closing", _inactivityTimeout.TotalMinutes);
            await CloseConnectionAsync();
        }
    }

    private async Task TestAndRecoverConnectionAsync()
    {
        try
        {
            SmtpClient? clientToCheck;
            lock (_connectionSemaphore)
            {
                clientToCheck = _smtpClient;
            }

            if (clientToCheck is { IsConnected: true, IsAuthenticated: true })
            {
                // 使用 NoOp 命令测试现有连接的真实健康状况
                var isHealthy = await TestExistingConnectionAsync(clientToCheck);
                if (!isHealthy)
                {
                    _logger.LogWarning("Existing connection test failed, attempting recovery");
                    await ResetConnectionAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection health");
        }
    }

    /// <summary>
    /// 测试现有连接是否健康（发送 NOOP 命令）
    /// </summary>
    private async Task<bool> TestExistingConnectionAsync(SmtpClient client)
    {
        try
        {
            await client.NoOpAsync(_monitorCts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NOOP command failed on existing connection");
            return false;
        }
    }

    /// <summary>
    /// 创建并连接新的 SMTP 客户端。调用方必须持有 _connectionSemaphore。
    /// </summary>
    private async Task CreateAndConnectClientAsync()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Creating new SMTP connection");

        var client = new SmtpClient();
        try
        {
            client.Timeout = _smtpSettings.Timeout;

            await client.ConnectAsync(
                _smtpSettings.Server,
                _smtpSettings.Port,
                GetSocketOptions(),
                _monitorCts.Token);

            _logger.LogInformation("SMTP connected to {Server}:{Port}",
                _smtpSettings.Server, _smtpSettings.Port);

            // 认证
            if (!string.IsNullOrEmpty(_smtpSettings.Username) && !string.IsNullOrEmpty(_smtpSettings.Password))
            {
                await client.AuthenticateAsync(
                    _smtpSettings.Username,
                    _smtpSettings.Password,
                    _monitorCts.Token);
                _logger.LogInformation("SMTP authentication successful");
            }

            // 替换旧连接（在信号量保护下）
            var oldClient = Interlocked.Exchange(ref _smtpClient, client);
            _lastActivity = DateTime.UtcNow;

            // 异步释放旧连接
            if (oldClient != null)
            {
                _ = DisposeOldClientAsync(oldClient);
            }
        }
        catch
        {
            client.Dispose();
            _logger.LogError("Failed to create SMTP connection");
            throw new SmtpConnectionException("Failed to establish SMTP connection");
        }
    }

    /// <summary>
    /// 异步释放旧客户端，避免阻塞当前操作
    /// </summary>
    private async Task DisposeOldClientAsync(SmtpClient oldClient)
    {
        try
        {
            if (oldClient.IsConnected)
            {
                await oldClient.DisconnectAsync(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting old SMTP client");
        }
        finally
        {
            oldClient.Dispose();
        }
    }

    public async Task CloseConnectionAsync()
    {
        ThrowIfDisposed();

        SmtpClient? clientToDispose;
        lock (_connectionSemaphore)
        {
            clientToDispose = _smtpClient;
            _smtpClient = null;
        }

        if (clientToDispose == null) return;

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
            _lastActivity = DateTime.UtcNow;
        }
    }

    private SecureSocketOptions GetSocketOptions() =>
        _smtpSettings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    public bool IsConnected
    {
        get
        {
            lock (_connectionSemaphore)
            {
                return _smtpClient is { IsConnected: true };
            }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (_connectionSemaphore)
            {
                return _smtpClient is { IsAuthenticated: true };
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

        // 同步关闭连接（Dispose 模式下的必要妥协）
        try
        {
            CloseConnectionAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing connection during dispose");
        }

        _monitorCts.Dispose();
        _connectionSemaphore.Dispose();

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Disposing SmtpClientFactory (async)");

        _monitorCts.Cancel();
        try
        {
            if (_monitorTask != null)
                await _monitorTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Monitor task did not complete within timeout");
        }

        await CloseConnectionAsync();

        _monitorCts.Dispose();
        _connectionSemaphore.Dispose();

        GC.SuppressFinalize(this);
    }

    ~SmtpClientFactory()
    {
        Dispose();
    }
}

public class SmtpConnectionException : Exception
{
    public SmtpConnectionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
