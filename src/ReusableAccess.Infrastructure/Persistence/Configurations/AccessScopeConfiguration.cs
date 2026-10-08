using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Infrastructure.Persistence.Configurations;

public sealed class AccessScopeConfiguration
    : IEntityTypeConfiguration<AccessScope>
{
    public void Configure(EntityTypeBuilder<AccessScope> builder)
    {
        builder.ToTable("AccessScopes");

        builder.HasKey(scope => scope.Id);

        builder.Property(scope => scope.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(scope => scope.Code)
            .IsUnique();

        builder.Property(scope => scope.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne<AccessScope>()
            .WithMany()
            .HasForeignKey(scope => scope.ParentScopeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}