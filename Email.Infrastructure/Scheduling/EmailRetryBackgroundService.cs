using Email.Domain.IApplication;
using Email.Domain.IRepository;
using Email.Extension.Option;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Email.Infrastructure.Scheduling;

/// <summary>
/// 失败邮件重试调度：周期扫描处于 Failed / Retry 状态的发送记录，
/// 交给 IDomainService 按统一策略重试（次数与冷却时间由领域层校验）。
/// </summary>
public class EmailRetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RetrySettings _settings;
    private readonly ILogger<EmailRetryBackgroundService> _logger;

    public EmailRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        RetrySettings settings,
        ILogger<EmailRetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("邮件重试调度未启用（RetrySettings:Enabled=false）");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _settings.IntervalSeconds));
        _logger.LogInformation(
            "邮件重试调度已启动：扫描间隔 {Interval}s，最大重试 {MaxRetryCount} 次，冷却 {Cooldown} 分钟，单轮上限 {BatchSize}",
            interval.TotalSeconds, _settings.MaxRetryCount, _settings.CooldownMinutes, _settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await RetryPendingEmailsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 单轮失败不影响后续调度
                _logger.LogError(ex, "邮件重试调度本轮执行出现异常");
            }
        }

        _logger.LogInformation("邮件重试调度已停止");
    }

    private async Task RetryPendingEmailsAsync(CancellationToken cancellationToken)
    {
        List<Guid> pendingIds;

        // 只用一个短生命周期的作用域取待重试列表
        // 必须用 CreateAsyncScope：作用域里的 FtpService 只实现了 IAsyncDisposable，
        // 同步释放会抛 InvalidOperationException。
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
            var records = await repository.GetRetryableRecordsAsync(
                _settings.MaxRetryCount, _settings.BatchSize, cancellationToken);
            pendingIds = records.Select(r => r.EmailMessageId).ToList();
        }

        if (pendingIds.Count == 0)
            return;

        _logger.LogInformation("发现 {Count} 封待重试邮件", pendingIds.Count);

        foreach (var emailId in pendingIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // 每封邮件独立作用域：单封失败不会污染后续重试的 DbContext 状态
                await using var scope = _scopeFactory.CreateAsyncScope();
                var domainService = scope.ServiceProvider.GetRequiredService<IDomainService>();
                await domainService.RetryEmailAsync(emailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件 {EmailId} 重试过程出现异常", emailId);
            }
        }
    }
}
