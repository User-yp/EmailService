using Email.Domain;
using Email.Domain.Entity;
using Xunit;

namespace Email.Tests.Domain;

/// <summary>
/// 发送记录的重试策略：次数上限、冷却时间、状态流转与错误信息截断。
/// </summary>
public class EmailRecordTests
{
    private static EmailRecord NewRecord() => new(Guid.NewGuid());

    [Fact]
    public void NewRecord_IsInitWithZeroRetry()
    {
        var record = NewRecord();

        Assert.Equal(EmailStatus.Init, record.Status);
        Assert.Equal(0, record.RetryCount);
        Assert.Empty(record.FailedAdress!);
        Assert.Null(record.SentTime);
        Assert.Null(record.FailedTime);
    }

    [Fact]
    public void MarkAsSent_SetsStatusAndClearsPreviousError()
    {
        var record = NewRecord();
        record.MarkAsFailed("boom", "detail");

        record.MarkAsSent();

        Assert.Equal(EmailStatus.Sent, record.Status);
        Assert.NotNull(record.SentTime);
        Assert.Null(record.ErrorMessage);
        Assert.Null(record.ErrorDetails);
    }

    [Fact]
    public void MarkAsFailed_RecordsErrorAndFailureTime()
    {
        var record = NewRecord();

        record.MarkAsFailed("smtp 550", "detail");

        Assert.Equal(EmailStatus.Failed, record.Status);
        Assert.Equal("smtp 550", record.ErrorMessage);
        Assert.Equal("detail", record.ErrorDetails);
        Assert.NotNull(record.FailedTime);
    }

    [Fact]
    public void MarkAsFailed_TruncatesErrorToDatabaseColumnLimits()
    {
        var record = NewRecord();

        record.MarkAsFailed(new string('m', 600), new string('d', 5000));

        // ErrorMessage=500 / ErrorDetails=4000，超长堆栈必须先截断再入库
        Assert.Equal(500, record.ErrorMessage!.Length);
        Assert.Equal(4000, record.ErrorDetails!.Length);
    }

    [Fact]
    public void MarkForRetry_TruncatesErrorToDatabaseColumnLimits()
    {
        var record = NewRecord();

        record.MarkForRetry(["a@b.com"], new string('m', 600), new string('d', 5000));

        Assert.Equal(500, record.ErrorMessage!.Length);
        Assert.Equal(4000, record.ErrorDetails!.Length);
    }

    [Fact]
    public void CanRetry_IsFalseForNonFailedStatus()
    {
        Assert.False(NewRecord().CanRetry());
    }

    [Fact]
    public void CanRetry_IsTrueForFailedWithinLimits()
    {
        var record = NewRecord();
        record.MarkAsFailed("boom");

        Assert.True(record.CanRetry(maxRetryCount: 3, cooldownPeriod: TimeSpan.Zero));
    }

    [Fact]
    public void CanRetry_IsFalseWhenMaxRetryCountReached()
    {
        var record = NewRecord();
        record.MarkAsFailed("boom");
        record.MarkForRetry(["a@b.com"]);

        Assert.False(record.CanRetry(maxRetryCount: 1, cooldownPeriod: TimeSpan.Zero));
    }

    [Fact]
    public void CanRetry_IsFalseDuringCooldownPeriod()
    {
        var record = NewRecord();
        record.MarkAsFailed("boom");
        record.MarkForRetry(["a@b.com"]);

        Assert.False(record.CanRetry(maxRetryCount: 3, cooldownPeriod: TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void CanRetry_IsTrueOnceCooldownElapsed()
    {
        var record = NewRecord();
        record.MarkAsFailed("boom");
        record.MarkForRetry(["a@b.com"]);

        // 冷却时间传 0 等价于"冷却期已过"
        Assert.True(record.CanRetry(maxRetryCount: 3, cooldownPeriod: TimeSpan.Zero));
    }

    [Fact]
    public void MarkForRetry_IncrementsCountAndDeduplicatesFailedAddresses()
    {
        var record = NewRecord();
        record.MarkAsFailed("boom");

        record.MarkForRetry(["a@b.com", "A@B.COM", "c@d.com"]);
        record.MarkForRetry(["a@b.com"]);

        Assert.Equal(2, record.RetryCount);
        Assert.NotNull(record.LastRetryTime);
        Assert.Equal(EmailStatus.Retry, record.Status);
        // 大小写不敏感去重，重复地址不再堆积
        Assert.Equal(new[] { "a@b.com", "c@d.com" }, record.FailedAdress!);
    }

    [Fact]
    public void MarkForRetry_AcceptsNullFailedAddresses()
    {
        var record = NewRecord();
        record.MarkAsFailed("boom");

        record.MarkForRetry(null, "err");

        Assert.Equal(EmailStatus.Retry, record.Status);
        Assert.Equal(1, record.RetryCount);
        Assert.Empty(record.FailedAdress!);
    }

    [Fact]
    public void ResetRetryCount_ClearsCounterAndCooldown()
    {
        var record = NewRecord();
        record.MarkForRetry(["a@b.com"]);

        record.ResetRetryCount();

        Assert.Equal(0, record.RetryCount);
        Assert.Null(record.LastRetryTime);
    }

    [Fact]
    public void GetSummary_ContainsStatusAndRetryCount()
    {
        var record = NewRecord();
        record.MarkAsFailed("boom");

        var summary = record.GetSummary();

        Assert.Contains("Failed", summary);
        Assert.Contains("Retries: 0", summary);
    }
}
