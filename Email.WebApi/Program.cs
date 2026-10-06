using Email.Infrastructure;
using Email.RPCServe;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGRpc();
// Add services to the container.
builder.Services.InitService(builder.Configuration);
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 监听端口必须显式声明：只要调用了 Listen*，hosting 的 applicationUrl / ASPNETCORE_URLS 就会被覆盖，
// 原先只声明 gRPC 端口（6102）会让 REST 与 Swagger 完全没有监听。
var grpcPort = builder.Configuration.GetValue("Ports:Grpc", 6102);
var httpPort = builder.Configuration.GetValue("Ports:Http", 5105);
var httpsPort = builder.Configuration.GetValue("Ports:Https", 7234);

builder.WebHost.ConfigureKestrel(options =>
{
    // gRPC：明文 HTTP/2（h2c），供 gRPC 客户端直连
    options.ListenAnyIP(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    // REST / Swagger：HTTP/1.1
    options.ListenAnyIP(httpPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });

    // REST / Swagger：HTTPS（把 Ports:Https 配成 0 可关闭）
    if (httpsPort > 0)
    {
        var certificate = FindDevelopmentCertificate();
        if (certificate == null)
        {
            Console.Error.WriteLine(
                $"[EmailService] 警告：未找到 localhost 开发证书，已跳过 HTTPS 端口 {httpsPort}。" +
                "执行 dotnet dev-certs https 可生成开发证书。");
        }
        else
        {
            options.ListenAnyIP(httpsPort, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                listenOptions.UseHttps(certificate);
            });
        }
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 只对 REST 请求做 HTTPS 跳转：gRPC 走明文 h2c，被 307 跳转后客户端会直接失败
app.UseWhen(context => !IsGrpcRequest(context), branch => branch.UseHttpsRedirection());

app.UseAuthorization();

app.MapControllers();
app.AddGRpc();

app.Run();

static bool IsGrpcRequest(HttpContext context)
{
    var contentType = context.Request.ContentType;
    return contentType != null
        && contentType.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase);
}

static X509Certificate2? FindDevelopmentCertificate()
{
    try
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        var now = DateTime.Now;
        return store.Certificates
            .OfType<X509Certificate2>()
            .Where(c => c.HasPrivateKey
                        && c.Subject.Contains("CN=localhost", StringComparison.OrdinalIgnoreCase)
                        && c.NotBefore <= now && c.NotAfter >= now)
            .OrderByDescending(c => c.NotAfter)
            .FirstOrDefault();
    }
    catch
    {
        return null;
    }
}
