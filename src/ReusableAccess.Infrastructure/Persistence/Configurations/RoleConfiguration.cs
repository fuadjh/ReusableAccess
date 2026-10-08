using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(role => role.Code)
            .IsUnique();

        builder.Property(role => role.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(role => role.Permissions)
            .HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}