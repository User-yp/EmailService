
using Email.Domain.IApplication;
using Email.Domain.IRepository;
using Email.Extension;
using Email.Extension.Option;
using Email.Infrastructure.Application;
using Email.Infrastructure.Factory;
using Email.Infrastructure.Repository;
using Email.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Email.Infrastructure;

public static class RegistExtension
{
    public static IServiceCollection InitService(this IServiceCollection service, IConfiguration configuration)
    {
        service.LoadConfiguration(configuration);

        // 重试调度配置（来自 appsettings.json，非 Redis 动态配置）
        var retrySettings = configuration.GetSection(nameof(RetrySettings)).Get<RetrySettings>() ?? new RetrySettings();
        service.AddSingleton(retrySettings);
        service.AddHostedService<EmailRetryBackgroundService>();

        service.AddDbContextFactory<EmailDbContext>((pro, opt) =>
        {
            var conn = pro.GetRequiredService<ConnectionOption>().ConnectionString;
            opt.UseSqlServer(conn);
        });
        service.AutoInJectService();
        return service;
    }
}
