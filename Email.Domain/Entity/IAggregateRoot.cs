namespace Email.Domain.Entity;

public class IAggregateRoot
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.Now;
    public DateTime UpdatedAt { get; protected set; } = DateTime.Now;
    public bool IsDeleted { get; protected set; } = false;

    public void UpdateTimestamp()
    {
        UpdatedAt = DateTime.Now;
    }
}