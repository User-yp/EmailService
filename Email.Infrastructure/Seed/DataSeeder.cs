using Email.Domain.Entity;
using Email.Domain.IApplication;
using Email.Domain.Models;
using Email.Extension.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Email.Infrastructure.Seed;

/// <summary>
/// 开发/演示用测试数据生成器：直接写库、不经过 SMTP，因此不会真的发信。
/// 默认在库中已有数据时跳过，避免重复污染开发库。
/// </summary>
[Service(ServiceLifetime.Scoped)]
public class DataSeeder : IDataSeeder
{
    /// <summary>测试数据统一使用的保留域名，便于用 SQL 一次性清理。</summary>
    private const string SampleDomain = "@example.com";

    private static readonly string[] Subjects =
    [
        "欢迎加入服务通知",
        "每月账单已生成",
        "登录验证码",
        "订单发货提醒",
        "密码重置确认",
        "系统维护公告",
        "本周交付周报",
        "异常告警：接口错误率上升"
    ];

    private static readonly string[] RecipientNames =
    [
        "alice", "bob", "carol", "dave", "erin", "frank", "grace", "henry"
    ];

    private static readonly string[] FailureReasons =
    [
        "SMTP 550 5.1.1 收件人地址不存在",
        "SMTP 421 服务暂时不可用，请稍后重试",
        "连接超时：SMTP 服务器未在 30s 内响应"
    ];

    private readonly IDbContextFactory<EmailDbContext> _dbContextFactory;
    private readonly ILogger<DataSeeder> _logger;
    private readonly Random _random = new();

    public DataSeeder(IDbContextFactory<EmailDbContext> dbContextFactory, ILogger<DataSeeder> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<SeedResult> SeedAsync(int count = 24, bool append = false, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 200);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.EmailMessages.CountAsync(cancellationToken);

        if (existing > 0 && !append)
        {
            _logger.LogInformation("数据库已有 {Count} 封邮件，跳过测试数据生成", existing);
            return new SeedResult
            {
                Skipped = true,
                ExistingCount = existing,
                Message = $"数据库中已有 {existing} 封邮件，未生成测试数据（勾选「追加」可强制生成）。"
            };
        }

        var now = DateTime.Now;
        var messages = new List<EmailMessage>(count);
        var createdTimes = new List<DateTime>(count);

        for (var i = 0; i < count; i++)
        {
            messages.Add(BuildSample(i, now));
            // 创建时间铺开到最近 14 天，让列表与统计更接近真实使用场景
            createdTimes.Add(now
                .AddDays(-(count - i) * 14.0 / count)
                .AddMinutes(-_random.Next(0, 240))
                .AddSeconds(-i));
        }

        context.EmailMessages.AddRange(messages);

        // CreatedAt/UpdatedAt 是聚合根的受保护属性，这里通过 EF 的当前值访问器写入
        for (var i = 0; i < messages.Count; i++)
        {
            var entry = context.Entry(messages[i]);
            entry.Property(nameof(AggregateRoot.CreatedAt)).CurrentValue = createdTimes[i];
            entry.Property(nameof(AggregateRoot.UpdatedAt)).CurrentValue = createdTimes[i];
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("已生成 {Count} 封测试邮件", messages.Count);

        return new SeedResult
        {
            Created = messages.Count,
            ExistingCount = existing,
            Message = $"已生成 {messages.Count} 封测试邮件（收件人均为 {SampleDomain} 演示地址）。"
        };
    }

    private EmailMessage BuildSample(int index, DateTime now)
    {
        var recipients = new List<string>();
        var recipientCount = 1 + _random.Next(0, 2);
        for (var i = 0; i < recipientCount; i++)
        {
            recipients.Add($"{RecipientNames[_random.Next(RecipientNames.Length)]}{index}{SampleDomain}");
        }

        var subject = $"{Subjects[index % Subjects.Length]}#{index + 1:D3}";
        var isHtml = index % 4 == 0;
        var body = isHtml
            ? $"<h3>{subject}</h3><p>这是一封由测试数据生成器写入的 <b>HTML</b> 示例邮件，用于验证前端展示效果。</p>"
            : $"你好：\n这是一封由测试数据生成器写入的示例邮件（序号 {index + 1}）。\n—— EmailService";

        var attachments = index % 3 == 0 ? BuildAttachments(index) : null;
        var message = EmailMessage.Create(recipients, null, null, $"noreply{SampleDomain}", subject, body, attachments, isHtml);

        // 让各种状态都有样本，便于验证列表筛选、重试与失败展示
        switch (index % 10)
        {
            case 0:
                message.MarkAsFailed(FailureReasons[index % FailureReasons.Length], $"测试数据：种子失败于 {now:yyyy-MM-dd HH:mm:ss}");
                break;
            case 3:
                message.MarkAsFailed(FailureReasons[index % FailureReasons.Length], "测试数据：首次失败");
                message.MarkForRetry(recipients, "测试数据：已排入重试", "等待后台重试调度处理");
                break;
            case 7:
                // 保持 Init：模拟尚未发送
                break;
            default:
                message.MarkAsSent();
                break;
        }

        return message;
    }

    private List<Attachment> BuildAttachments(int index)
    {
        var attachments = new List<Attachment>();
        var count = 1 + index % 2;

        for (var i = 0; i < count; i++)
        {
            var (fileName, contentType, content) = (i % 3) switch
            {
                0 => ($"示例文本-{index}-{i + 1}.txt", "text/plain",
                    Encoding.UTF8.GetBytes($"示例附件内容（{index}-{i + 1}）\n生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}")),
                1 => ($"data-{index}-{i + 1}.json", "application/json",
                    Encoding.UTF8.GetBytes($"{{\"index\": {index}, \"item\": {i + 1}, \"source\": \"seeder\"}}")),
                _ => ($"报表-{index}-{i + 1}.csv", "text/csv",
                    Encoding.UTF8.GetBytes("name,count\nalice,1\nbob,2\n"))
            };

            attachments.Add(Attachment.Create(fileName, contentType, content));
        }

        return attachments;
    }
}
