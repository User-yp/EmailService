using Email.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Email.Infrastructure.Config;

internal class EmailRecordConfig : IEntityTypeConfiguration<EmailRecord>
{
    public void Configure(EntityTypeBuilder<EmailRecord> builder)
    {
        builder.ToTable(nameof(EmailRecord));

        builder.HasKey(e => e.Id);

        // 状态字段配置
        builder.Property(e => e.Status)
              .IsRequired()
              .HasConversion<string>();

        builder.Property(e => e.RetryCount)
              .IsRequired();

        builder.Property(e => e.ErrorMessage)
              .HasMaxLength(500);

        builder.Property(e => e.ErrorDetails)
              .HasMaxLength(4000);

        builder.Property(e => e.CreatedAt)
              .IsRequired();

        builder.Property(e => e.UpdatedAt)
              .IsRequired();

        // 索引配置
        builder.HasIndex(r => r.EmailMessageId)
              .IsUnique(); // 一对一关系需要唯一索引

        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => r.UpdatedAt);

        builder.HasQueryFilter(e => e.IsDeleted == false);
    }
}
