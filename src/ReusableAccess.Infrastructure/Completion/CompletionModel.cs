using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Identity;
using System.Reflection.Emit;

namespace ReusableAccess.Infrastructure.Completion;

public static class CompletionModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(100).HasDefaultValue("");
            e.Property(x => x.LastName).HasMaxLength(100).HasDefaultValue("");
            e.Property(x => x.OrganizationalCode).HasMaxLength(50).HasDefaultValue("");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.SessionVersion).HasDefaultValue(0);
            e.Property(x => x.MustChangePassword).HasDefaultValue(false);
        });

        b.Entity<AuthenticationPolicy>(e =>
        {
            e.ToTable("AuthenticationPolicies");
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasData(new AuthenticationPolicy { Id = 1 });
        });

        b.Entity<AccountChallenge>(e =>
        {
            e.ToTable("AccountChallenges");
            e.HasKey(x => x.Id);
            e.Property(x => x.Purpose).HasMaxLength(50);
            e.Property(x => x.ProtectedCode).HasMaxLength(2048);
            e.Property(x => x.Version).IsConcurrencyToken();

            e.Property(x => x.CreatedUtc).HasConversion(
                v => v.UtcDateTime,
                v => new DateTimeOffset(
                    DateTime.SpecifyKind(v, DateTimeKind.Utc)));

            e.Property(x => x.ExpiresUtc).HasConversion(
                v => v.UtcDateTime,
                v => new DateTimeOffset(
                    DateTime.SpecifyKind(v, DateTimeKind.Utc)));

            e.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedUtc });
        });

        b.Entity<SecurityAuditEntry>(e =>
        {
            e.ToTable("SecurityAuditEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.Entity).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(200);
            e.Property(x => x.ChangedFields).HasMaxLength(1500);

            e.Property(x => x.OccurredUtc).HasConversion(
                v => v.UtcDateTime,
                v => new DateTimeOffset(
                    DateTime.SpecifyKind(v, DateTimeKind.Utc)));

            e.HasIndex(x => x.OccurredUtc);
        });
    }
}