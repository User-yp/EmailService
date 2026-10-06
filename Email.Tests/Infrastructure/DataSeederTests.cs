using Email.Domain;
using Email.Domain.IApplication;
using Email.Infrastructure;
using Email.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Email.Tests.Infrastructure;

/// <summary>
/// 测试数据生成器：状态分布、附件、创建时间铺开与幂等策略。
/// </summary>
public class DataSeederTests
{
    private static (IDataSeeder Seeder, IDbContextFactory<EmailDbContext> Factory) Create()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<EmailDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<EmailDbContext>>();

        return (new DataSeeder(factory, NullLogger<DataSeeder>.Instance), factory);
    }

    [Fact]
    public async Task SeedAsync_CreatesRequestedNumberOfMessagesWithMixedStatuses()
    {
        var (seeder, factory) = Create();

        var result = await seeder.SeedAsync(count: 30, append: false);

        Assert.False(result.Skipped);
        Assert.Equal(30, result.Created);

        await using var context = await factory.CreateDbContextAsync();
        var records = await context.EmailRecords.ToListAsync();

        Assert.Equal(30, await context.EmailMessages.CountAsync());
        Assert.Equal(30, records.Count);
        Assert.Contains(records, r => r.Status == EmailStatus.Sent);
        Assert.Contains(records, r => r.Status == EmailStatus.Failed);
        Assert.Contains(records, r => r.Status == EmailStatus.Retry);
        Assert.Contains(records, r => r.Status == EmailStatus.Init);
        Assert.Contains(records, r => r.RetryCount > 0);
    }

    [Fact]
    public async Task SeedAsync_SpreadsCreatedAtOverRecentDays()
    {
        var (seeder, factory) = Create();

        await seeder.SeedAsync(count: 20);

        await using var context = await factory.CreateDbContextAsync();
        var createdTimes = await context.EmailMessages.Select(m => m.CreatedAt).ToListAsync();

        Assert.True(createdTimes.Max() - createdTimes.Min() > TimeSpan.FromDays(7));
        Assert.True(createdTimes.Max() <= DateTime.Now.AddMinutes(1));
    }

    [Fact]
    public async Task SeedAsync_CreatesAttachmentsWithContentAndByteSizes()
    {
        var (seeder, factory) = Create();

        await seeder.SeedAsync(count: 12);

        await using var context = await factory.CreateDbContextAsync();
        var attachments = await context.EmailAttachments.ToListAsync();

        Assert.NotEmpty(attachments);
        Assert.All(attachments, a =>
        {
            Assert.NotEmpty(a.Content);
            Assert.Equal(a.Content.Length, a.FileSize);
            Assert.False(a.IsStoredInFtp);
        });
    }

    [Fact]
    public async Task SeedAsync_SkipsWhenDatabaseAlreadyHasData()
    {
        var (seeder, factory) = Create();
        await seeder.SeedAsync(count: 5);

        var second = await seeder.SeedAsync(count: 10);

        Assert.True(second.Skipped);
        Assert.Equal(0, second.Created);
        Assert.Equal(5, second.ExistingCount);

        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(5, await context.EmailMessages.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_AppendTrue_AddsMoreMessages()
    {
        var (seeder, factory) = Create();
        await seeder.SeedAsync(count: 5);

        var second = await seeder.SeedAsync(count: 3, append: true);

        Assert.False(second.Skipped);
        Assert.Equal(3, second.Created);

        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(8, await context.EmailMessages.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_ClampsCountIntoSupportedRange()
    {
        var (seeder, factory) = Create();

        var result = await seeder.SeedAsync(count: 9999);

        Assert.Equal(200, result.Created);

        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(200, await context.EmailMessages.CountAsync());
    }
}
