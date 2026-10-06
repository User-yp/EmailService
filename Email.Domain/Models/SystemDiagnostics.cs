namespace Email.Domain.Models;

/// <summary>
/// 依赖组件（SMTP / FTP / 数据库 / Redis）的连通性诊断结果。
/// </summary>
public class SystemDiagnostics
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public DiagnosticItem Smtp { get; init; } = new();
    public DiagnosticItem Ftp { get; init; } = new();
    public DiagnosticItem Database { get; init; } = new();
    public DiagnosticItem Redis { get; init; } = new();

    public bool AllHealthy => Smtp.Success && Ftp.Success && Database.Success && Redis.Success;

    public IReadOnlyList<DiagnosticItem> Items => [Smtp, Ftp, Database, Redis];
}

public class DiagnosticItem
{
    public string Target { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public long ElapsedMilliseconds { get; init; }
}
