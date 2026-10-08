using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration
    : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Key)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(permission => permission.Key)
            .IsUnique();

        builder.Property(permission => permission.Description)
            .HasMaxLength(500)
            .IsRequired();
    }
}