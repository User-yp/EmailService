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
}
