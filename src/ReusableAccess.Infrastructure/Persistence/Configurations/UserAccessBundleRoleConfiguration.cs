using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Infrastructure.Persistence.Configurations;

public sealed class UserAccessBundleRoleConfiguration
    : IEntityTypeConfiguration<UserAccessBundleRole>
{
    public void Configure(EntityTypeBuilder<UserAccessBundleRole> builder)
    {
        builder.ToTable("UserAccessBundleRoles");

        builder.HasKey(item => new
        {
            item.BundleId,
            item.RoleId
        });

        builder.HasOne<UserAccessBundle>()
            .WithMany(bundle => bundle.Roles)
            .HasForeignKey(item => item.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(item => item.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}