using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Email.RPCServe;

public static class RPCExtension
{
    public static IServiceCollection AddGRpc(this IServiceCollection service)
    {
        service.AddGrpc(options =>
        {
            options.EnableDetailedErrors = true;
            // 配置消息大小限制（如果需要处理大文件）
            options.MaxReceiveMessageSize = 30 * 1024 * 1024; // 30MB
            options.MaxSendMessageSize = 30 * 1024 * 1024; // 30MB
        });
        return service;
    }
    public static void AddGRpc(this WebApplication app)
    {
        app.MapGrpcService<GrpcEmailService>();
    }
}
