namespace Email.Domain.Entity;

public class EmailRecord : AggregateRoot
{
    public Guid EmailMessageId { get; private set; }

    // 状态信息
    public EmailStatus Status { get; private set; }
    public List<string>? FailedAdress { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? LastRetryTime { get; private set; }
    public DateTime? SentTime { get; private set; }
    public DateTime? FailedTime { get; private set; }

    // 错误信息
    public string? ErrorMessage { get; private set; }
    public string? ErrorDetails { get; private set; }


    // 导航属性
    public EmailMessage EmailMessage { get; private set; }

    private EmailRecord() { }

    public EmailRecord(Guid emailMessageId)
    {
        EmailMessageId = emailMessageId;
        Status = EmailStatus.Init;
        FailedAdress = new List<string>();
        RetryCount = 0;
    }

    // 标记为发送成功
    public void MarkAsSent()
    {
        Status = EmailStatus.Sent;
        SentTime = DateTime.Now;
        ErrorMessage = null;
        ErrorDetails = null;
        UpdateTimestamp();
    }

    // 标记为发送失败
    public void MarkAsFailed(string? errorMessage = null, string? errorDetails = null)
    {
        Status = EmailStatus.Failed;
        FailedTime = DateTime.Now;
        ErrorMessage = Truncate(errorMessage, MaxErrorMessageLength);
        ErrorDetails = Truncate(errorDetails, MaxErrorDetailsLength);
        UpdateTimestamp();
    }

    // 重试邮件
    public void MarkForRetry(List<string>? failedAdress, string? errorMessage = null, string? errorDetails = null)
    {
        Status = EmailStatus.Retry;
        RetryCount++;
        LastRetryTime = DateTime.Now;
        ErrorMessage = Truncate(errorMessage, MaxErrorMessageLength);
        ErrorDetails = Truncate(errorDetails, MaxErrorDetailsLength);
        UpdateTimestamp();

        if (failedAdress is not { Count: > 0 })
            return;

        FailedAdress ??= new List<string>();
        foreach (var address in failedAdress)
        {
            // 避免多次重试后失败地址重复堆积
            if (!FailedAdress.Contains(address, StringComparer.OrdinalIgnoreCase))
                FailedAdress.Add(address);
        }
    }

    // 重置重试计数
    public void ResetRetryCount()
    {
        RetryCount = 0;
        LastRetryTime = null;
        UpdateTimestamp();
    }

    // 错误信息长度上限，与 EF 配置中 ErrorMessage/ErrorDetails 的最大长度保持一致，
    // 避免超长堆栈写库时被截断报错、掩盖真实失败原因。
    private const int MaxErrorMessageLength = 500;
    private const int MaxErrorDetailsLength = 4000;

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];

    // 是否可以重试（基于最大重试次数和冷却时间）
    public bool CanRetry(int maxRetryCount = 3, TimeSpan? cooldownPeriod = null)
    {
        if (Status != EmailStatus.Failed && Status != EmailStatus.Retry)
            return false;

        if (RetryCount >= maxRetryCount)
            return false;

        // 检查冷却时间（默认5分钟）
        cooldownPeriod ??= TimeSpan.FromMinutes(5);
        if (LastRetryTime.HasValue &&
            DateTime.Now - LastRetryTime.Value < cooldownPeriod.Value)
            return false;

        return true;
    }

    // 获取记录摘要
    public string GetSummary()
    {
        return $"Email {EmailMessageId}: {Status}, Retries: {RetryCount}, " +
               $"Last Updated: {UpdatedAt:yyyy-MM-dd HH:mm:ss}";
    }
}
