using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;
namespace ReusableAccess.Infrastructure.Persistence.Configurations;
public sealed class SecurityAuditEntryConfiguration : IEntityTypeConfiguration<SecurityAuditEntry>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEntry> builder)
    {
        builder.ToTable("SecurityAuditEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(100);
        builder.Property(x => x.Entity).HasMaxLength(100);
        builder.Property(x => x.EntityId).HasMaxLength(200);
        builder.Property(x => x.ChangedFields).HasMaxLength(1500);
        builder.Property(x => x.OccurredUtc).HasConversion(v => v.UtcDateTime, v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));
        builder.HasIndex(x => x.OccurredUtc);
    }
}
