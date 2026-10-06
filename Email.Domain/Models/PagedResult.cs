namespace Email.Domain.Models;

/// <summary>
/// 分页查询结果。
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>满足条件的总条数（不是当前页条数）。</summary>
    public int Total { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new() { Page = page, PageSize = pageSize };
}
