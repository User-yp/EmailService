namespace Email.Domain.Entity;

public class AggregateRoot
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.Now;
    public DateTime UpdatedAt { get; protected set; } = DateTime.Now;
    public bool IsDeleted { get; protected set; } = false;

    public void UpdateTimestamp()
    {
        UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 软删除：仅置位删除标记，数据物理保留（配合全局查询过滤器生效）。
    /// </summary>
    public virtual void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdateTimestamp();
    }

    /// <summary>
    /// 取消软删除。
    /// </summary>
    public virtual void Restore()
    {
        IsDeleted = false;
        UpdateTimestamp();
    }
}
