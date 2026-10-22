using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Persistence.Configurations;

public sealed class UserAccessBundleConfiguration
    : IEntityTypeConfiguration<UserAccessBundle>
{
    public void Configure(EntityTypeBuilder<UserAccessBundle> builder)
    {
        builder.ToTable("UserAccessBundles");
        builder.Property(bundle => bundle.SortOrder)
    .IsRequired();

        builder.HasIndex(bundle => new
        {
            bundle.UserId,
            bundle.SortOrder
        });
        builder.HasKey(bundle => bundle.Id);

        builder.Property(bundle => bundle.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(bundle => bundle.ScopeSelectionMode)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(bundle => bundle.IsEnabled)
            .IsRequired();

        builder.HasIndex(bundle => new
        {
            bundle.UserId,
            bundle.IsEnabled
        });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(bundle => bundle.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(bundle => bundle.Roles)
            .WithOne()
            .HasForeignKey(role => role.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(bundle => bundle.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(bundle => bundle.Scopes)
            .WithOne()
            .HasForeignKey(scope => scope.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(bundle => bundle.Scopes)
            .HasField("_scopes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}