using Email.Domain.IApplication;
using Email.Domain.IRepository;
using Email.Domain.Models;
using Email.Extension.Attributes;
using Email.Extension.Option;
using Email.Infrastructure.Factory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;

namespace Email.Infrastructure.Application;

/// <summary>
/// 依赖组件连通性诊断：SMTP / FTP / 数据库 / Redis。
/// </summary>
[Service(ServiceLifetime.Scoped)]
public class SystemDiagnosticsService : ISystemDiagnosticsService
{
    private readonly SmtpClientFactory _smtpClientFactory;
    private readonly IFtpService _ftpService;
    private readonly IDbContextFactory<EmailDbContext> _dbContextFactory;
    private readonly RedisOption _redisOption;
    private readonly ILogger<SystemDiagnosticsService> _logger;

    public SystemDiagnosticsService(
        SmtpClientFactory smtpClientFactory,
        IFtpService ftpService,
        IDbContextFactory<EmailDbContext> dbContextFactory,
        RedisOption redisOption,
        ILogger<SystemDiagnosticsService> logger)
    {
        _smtpClientFactory = smtpClientFactory;
        _ftpService = ftpService;
        _dbContextFactory = dbContextFactory;
        _redisOption = redisOption;
        _logger = logger;
    }

    public async Task<SystemDiagnostics> CheckAsync(CancellationToken cancellationToken = default)
    {
        var smtp = await ProbeAsync("SMTP", async () =>
        {
            var ok = await _smtpClientFactory.TestConnectionAsync();
            return (ok, ok ? "SMTP 连接与认证正常" : "SMTP 连接或认证失败，详见日志");
        });

        var ftp = await ProbeAsync("FTP", async () =>
        {
            var ok = await _ftpService.TestConnectionAsync();
            return (ok, ok ? "FTP 连接正常" : "FTP 连接失败，详见日志");
        });

        var database = await ProbeAsync("Database", async () =>
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var ok = await context.Database.CanConnectAsync(cancellationToken);
            return (ok, ok ? "数据库可连接" : "数据库不可连接");
        });

        var redis = await ProbeAsync("Redis", async () =>
        {
            using var connection = await ConnectionMultiplexer.ConnectAsync(_redisOption.ConnectionString);
            var latency = await connection.GetDatabase(_redisOption.DbNumber).PingAsync();
            return (true, $"Redis PING 往返 {latency.TotalMilliseconds:F0} ms");
        });

        return new SystemDiagnostics { Smtp = smtp, Ftp = ftp, Database = database, Redis = redis };
    }

    /// <summary>
    /// 执行一次探测：返回 (是否通过, 说明)；抛异常时按失败处理。
    /// </summary>
    private async Task<DiagnosticItem> ProbeAsync(string target, Func<Task<(bool Success, string Message)>> probe)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (success, message) = await probe();
            return new DiagnosticItem
            {
                Target = target,
                Success = success,
                Message = message,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Target} 连通性检查失败", target);
            return new DiagnosticItem
            {
                Target = target,
                Success = false,
                Message = $"{ex.GetType().Name}: {ex.Message}",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
