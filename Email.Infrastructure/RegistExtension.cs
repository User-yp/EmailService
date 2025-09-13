
using Email.Domain.IApplication;
using Email.Domain.IRepository;
using Email.Extension;
using Email.Extension.Option;
using Email.Infrastructure.Application;
using Email.Infrastructure.Factory;
using Email.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Email.Infrastructure;

public static class RegistExtension
{
    public static IServiceCollection InitService(this IServiceCollection service, IConfiguration configuration)
    {
        service.LoadConfiguration(configuration);
        /*service.AddScoped<DomainService>();
        service.AddScoped<IEmailHandler,EmailHandler>();
        service.AddScoped<IEmailRepository,EmailRepository>();
        service.AddScoped<IFtpService,FtpService>();
        service.AddSingleton<SmtpClientFactory>();*/

        /*var conn = configuration.GetSection(nameof(ConnectionOption)).Get<ConnectionOption>().ConnectionString
            ?? throw new ArgumentNullException(nameof(ConnectionOption), "ConnectionOption configuration is null or empty.");*/
        service.AddDbContextFactory<EmailDbContext>((pro, opt) =>
        {
            var conn= pro.GetRequiredService<ConnectionOption>().ConnectionString;
            opt.UseSqlServer(conn);
        });
        service.AutoInJectService();
        return service;
    }
}
