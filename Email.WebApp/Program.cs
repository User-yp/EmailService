using Email.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// WebApp 是继 WebApi / RPCServe 之后的第三个表现层，复用同一套领域与基础设施注册：
// Redis 动态配置、EF Core、SMTP / FTP、自动 DI 等统一由 InitService 装配。
builder.Services.InitService(builder.Configuration);

// 前端是 Vue 3 SPA（wwwroot 下免构建加载 vue / vue-router），后端只提供 JSON 接口。
// 枚举以字符串序列化，前端可直接拿到 "Sent" / "Failed" 这样的状态值。
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new { error = "服务器内部错误，请查看服务日志。" });
    }));
    app.UseHsts();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// 前端路由（/emails、/emails/{id} 等）交由 vue-router 处理，
// 未命中的非文件请求回落到 index.html；静态资源与 /api 不受影响。
app.MapFallbackToFile("{*path:nonfile}", "index.html");

app.Run();
