namespace Email.Domain.Models;

/// <summary>
/// 邮件列表查询条件。
/// </summary>
public class EmailQueryFilter
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    /// <summary>按发送状态过滤；null 表示全部。</summary>
    public EmailStatus? Status { get; set; }

    /// <summary>关键字，匹配主题或收件人。</summary>
    public string? Keyword { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>是否按创建时间倒序（默认最新在前）。</summary>
    public bool Descending { get; set; } = true;

    public int NormalizedPage => Math.Max(Page, 1);

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
}
