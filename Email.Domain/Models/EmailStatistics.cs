namespace Email.Domain.Models;

/// <summary>
/// 各状态邮件数量统计（首页概览用）。
/// </summary>
public class EmailStatistics
{
    public int Total { get; set; }
    public int Draft { get; set; }
    public int Init { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Retry { get; set; }

    /// <summary>全部邮件累计附件数。</summary>
    public int AttachmentCount { get; set; }

    /// <summary>最近一次成功发送时间。</summary>
    public DateTime? LastSentTime { get; set; }

    /// <summary>发送成功率（不含尚未发送的 Init/Draft）。</summary>
    public double SuccessRate
    {
        get
        {
            var attempted = Sent + Failed + Retry;
            return attempted == 0 ? 0 : Math.Round(Sent * 100d / attempted, 1);
        }
    }
}
