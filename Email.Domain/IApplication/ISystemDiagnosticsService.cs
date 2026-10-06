using Email.Domain.Models;

namespace Email.Domain.IApplication;

public interface ISystemDiagnosticsService
{
    /// <summary>
    /// 检查 SMTP / FTP / 数据库 / Redis 四项依赖的连通性。
    /// </summary>
    Task<SystemDiagnostics> CheckAsync(CancellationToken cancellationToken = default);
}
