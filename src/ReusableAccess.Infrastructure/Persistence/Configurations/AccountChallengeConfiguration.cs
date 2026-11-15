using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;
namespace ReusableAccess.Infrastructure.Persistence.Configurations;
public sealed class AccountChallengeConfiguration : IEntityTypeConfiguration<AccountChallenge>
{
    public void Configure(EntityTypeBuilder<AccountChallenge> builder)
    {
        builder.ToTable("AccountChallenges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Purpose).HasMaxLength(50);
        builder.Property(x => x.ProtectedCode).HasMaxLength(2048);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.CreatedUtc).HasConversion(v => v.UtcDateTime, v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));
        builder.Property(x => x.ExpiresUtc).HasConversion(v => v.UtcDateTime, v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedUtc });
    }
}
