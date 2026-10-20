using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Infrastructure.Persistence.Configurations;

public sealed class UserAccessBundleScopeConfiguration
    : IEntityTypeConfiguration<UserAccessBundleScope>
{
    public void Configure(EntityTypeBuilder<UserAccessBundleScope> builder)
    {
        builder.ToTable("UserAccessBundleScopes");

        builder.HasKey(item => new
        {
            item.BundleId,
            item.ScopeId
        });

        builder.HasOne<UserAccessBundle>()
            .WithMany(bundle => bundle.Scopes)
            .HasForeignKey(item => item.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AccessScope>()
            .WithMany()
            .HasForeignKey(item => item.ScopeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}