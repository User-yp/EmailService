namespace Email.Domain.Models;

/// <summary>
/// 测试数据生成结果。
/// </summary>
public class SeedResult
{
    /// <summary>本次新增的邮件数量。</summary>
    public int Created { get; init; }

    /// <summary>生成前数据库中已有的邮件数量。</summary>
    public int ExistingCount { get; init; }

    public bool Skipped { get; init; }

    public string Message { get; init; } = string.Empty;
}
