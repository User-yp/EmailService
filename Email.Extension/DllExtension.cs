using Email.Extension.Attributes;
using Email.Extension.Option;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Reflection;

namespace Email.Extension;

public static class DllExtension
{
    public static IServiceCollection LoadConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var opt = configuration.GetSection(nameof(RedisOption)).Get<RedisOption>()
            ?? throw new ArgumentNullException(nameof(RedisOption), "RedisOption configuration is null or empty.");
        // 创建临时连接
        var tempConnection = ConnectionMultiplexer.Connect(opt.ConnectionString);

        try
        {
            var db = tempConnection.GetDatabase(opt.DbNumber);
            var configs = db.HashGetAll(opt.ConfigKey).ToDictionary(item => item.Name.ToString(), item => item.Value.ToString());
            var optionTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => !t.IsAbstract && t.IsClass && t.GetCustomAttributes(typeof(OptionAttribute), false).Length != 0).ToList();

            foreach (var item in configs)
            {
                var type = optionTypes.FirstOrDefault(t => t.Name.Equals(item.Key.ToString(), StringComparison.OrdinalIgnoreCase));
                if (type == null)
                    continue;

                var config = JsonConvert.DeserializeObject(item.Value, type)
                    ?? throw new ArgumentNullException(nameof(item.Value), $"Configuration for {item.Key} is null or empty.");
                //将配置注入到容器里
                services.AddSingleton(type, config);
            }
        }
        catch (Exception ex)
        {
            // Redis 不可用时不应阻止应用启动，但需记录日志
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to load configuration from Redis: {ex.Message}");
        }
        finally
        {
            tempConnection.Close();
        }
        return services;
    }
    public static IServiceCollection AutoInJectService(this IServiceCollection service)
    {
        var serviceTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("Email", StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.IsClass && t.GetCustomAttributes(typeof(ServiceAttribute), false).Length != 0);

        foreach (var type in serviceTypes)
        {
            var lifetime = type.GetCustomAttribute<ServiceAttribute>()!.LifeTime;
            var interfaces = type.GetInterfaces().FirstOrDefault(t => t.Name.Contains(type.Name));
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    if (interfaces != null)
                        service.AddSingleton(interfaces, type);
                    else
                        service.AddSingleton(type);
                    break;
                case ServiceLifetime.Scoped:
                    if (interfaces != null)
                        service.AddScoped(interfaces, type);
                    else
                        service.AddScoped(type);
                    break;
                case ServiceLifetime.Transient:
                    if (interfaces != null)
                        service.AddTransient(interfaces, type);
                    else
                        service.AddTransient(type);
                    break;
            }
        }
        return service;
    }
}