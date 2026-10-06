using Email.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Email.Infrastructure;

public class EmailDbContext : DbContext
{
    public DbSet<EmailMessage> EmailMessages { get; set; }
    public DbSet<Attachment> EmailAttachments { get; set; }
    public DbSet<EmailRecord> EmailRecords { get; set; }
    public EmailDbContext(DbContextOptions<EmailDbContext> options) : base(options)
    {

    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    public override int SaveChanges()
    {
        ApplySoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 把物理删除改写为软删除：所有 AggregateRoot 实体的 Deleted 状态会被转成 Modified
    /// 并置位 IsDeleted，保证数据物理保留、级联删除不会真正落库。
    /// </summary>
    private void ApplySoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<AggregateRoot>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.MarkAsDeleted();
            }
        }
    }
}
