using Email.Domain.Models;

namespace Email.Domain.IApplication;

/// <summary>
/// 开发/演示用测试数据生成器（不经过 SMTP，不会真的发信）。
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// 生成测试邮件。默认在库中已有数据时跳过，<paramref name="append"/> 为 true 时强制追加。
    /// </summary>
    Task<SeedResult> SeedAsync(int count = 24, bool append = false, CancellationToken cancellationToken = default);
}
