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
    // 运行时必须存在的配置项：缺失就直接启动失败，避免请求期才暴露配置错误
    private static readonly Type[] RequiredOptionTypes =
    [
        typeof(ConnectionOption),
        typeof(SmtpSettings),
        typeof(FtpSettings)
    ];

    /// <summary>
    /// 加载配置：先绑定 appsettings.json 中的静态配置节，再用 Redis Hash 中的动态配置覆盖同名项。
    /// 两者都取不到必需项时直接抛异常（fail-fast），不再静默降级到"启动成功、请求时报错"。
    /// </summary>
    public static IServiceCollection LoadConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var optionTypes = GetOptionTypes();

        // 1) appsettings.json —— 例如 ConnectionOption；缺失的项留给 Redis 提供
        BindFromConfiguration(services, configuration, optionTypes);

        // 2) Redis Hash 动态配置 —— 同名项覆盖 appsettings
        foreach (var (key, json) in ReadDynamicConfigs(configuration))
        {
            var type = optionTypes.FirstOrDefault(t => t.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (type == null)
                continue;

            var config = JsonConvert.DeserializeObject(json, type)
                ?? throw new InvalidOperationException($"Redis 配置项 {key} 的内容无法反序列化为 {type.Name}。");
            // 将配置注入到容器里
            services.AddSingleton(type, config);
        }

        // 3) 必需配置必须齐全
        EnsureRequiredOptions(services);

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

    private static List<Type> GetOptionTypes() => Assembly.GetExecutingAssembly().GetTypes()
        .Where(t => !t.IsAbstract && t.IsClass && t.GetCustomAttributes(typeof(OptionAttribute), false).Length != 0)
        .ToList();

    /// <summary>
    /// 绑定 appsettings.json 中存在的配置节（按 [Option] 类名匹配）。
    /// </summary>
    private static void BindFromConfiguration(IServiceCollection services, IConfiguration configuration, List<Type> optionTypes)
    {
        foreach (var type in optionTypes)
        {
            var section = configuration.GetSection(type.Name);
            if (!section.Exists())
                continue;

            var instance = section.Get(type);
            if (instance != null)
                services.AddSingleton(type, instance);
        }
    }

    /// <summary>
    /// 读取 Redis Hash 中的动态配置；Redis 不可用时返回空集合并显式告警，
    /// 只要静态配置齐全仍可启动。
    /// </summary>
    private static Dictionary<string, string> ReadDynamicConfigs(IConfiguration configuration)
    {
        var redisOption = configuration.GetSection(nameof(RedisOption)).Get<RedisOption>()
            ?? throw new InvalidOperationException("缺少 RedisOption 配置节。");

        if (string.IsNullOrWhiteSpace(redisOption.ConnectionString))
            throw new InvalidOperationException("RedisOption:ConnectionString 未配置。");

        try
        {
            using var tempConnection = ConnectionMultiplexer.Connect(redisOption.ConnectionString);
            var db = tempConnection.GetDatabase(redisOption.DbNumber);
            return db.HashGetAll(redisOption.ConfigKey)
                .ToDictionary(item => item.Name.ToString(), item => item.Value.ToString());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[EmailService] 警告：读取 Redis 动态配置失败（{ex.GetType().Name}: {ex.Message}），" +
                "本次启动仅使用 appsettings.json 中的静态配置。");
            return new Dictionary<string, string>();
        }
    }

    private static void EnsureRequiredOptions(IServiceCollection services)
    {
        var missing = RequiredOptionTypes
            .Where(t => services.All(d => d.ServiceType != t))
            .Select(t => t.Name)
            .ToList();

        if (missing.Count == 0)
            return;

        throw new InvalidOperationException(
            $"缺少必需的配置：{string.Join("、", missing)}。" +
            "请在 appsettings.json 中补充对应配置节，或在 Redis Hash（RedisOption:ConfigKey）中写入同名字段。");
    }
}
