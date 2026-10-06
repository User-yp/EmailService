using Email.Extension.Attributes;

namespace Email.Extension.Option;

/// <summary>
/// 失败邮件重试调度配置。
/// 注意：该配置来自 appsettings.json（非 Redis 动态配置），因此未标记 [Option] 特性。
/// </summary>
[Option]
public class RetrySettings
{
    /// <summary>是否启用后台重试调度。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>扫描间隔（秒），最小 5 秒。</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>单封邮件最大重试次数。</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>两次重试之间的冷却时间（分钟）。</summary>
    public int CooldownMinutes { get; set; } = 5;

    /// <summary>单轮最多处理的邮件数量。</summary>
    public int BatchSize { get; set; } = 20;
}
