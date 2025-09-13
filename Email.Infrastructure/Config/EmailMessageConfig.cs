using Microsoft.EntityFrameworkCore;
using Email.Domain.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Email.Infrastructure.Config;

public class EmailMessageConfig : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable(nameof(EmailMessage));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.To).IsRequired();
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(255);
        builder.Property(e => e.Body).IsRequired();

        // 配置一对多关系
        builder.HasMany(e => e.Attachments)
              .WithOne(a => a.EmailMessage)
              .HasForeignKey(a => a.EmailMessageId)
              .OnDelete(DeleteBehavior.Cascade);

        // 配置一对一关系（记录）
        builder.HasOne(e => e.Record)
              .WithOne(r => r.EmailMessage)
              .HasForeignKey<EmailRecord>(r => r.EmailMessageId)
              .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => e.IsDeleted == false);
    }
}
