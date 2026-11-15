using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Infrastructure.Identity;
namespace ReusableAccess.Infrastructure.Persistence.Configurations;
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(100).HasDefaultValue("");
        builder.Property(u => u.LastName).HasMaxLength(100).HasDefaultValue("");
        builder.Property(u => u.OrganizationalCode).HasMaxLength(50).HasDefaultValue("");
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.SessionVersion).HasDefaultValue(0);
        builder.Property(u => u.MustChangePassword).HasDefaultValue(false);
        builder.Property(u => u.NationalId).HasMaxLength(50).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.HasIndex(u => u.NationalId).IsUnique();
    }
}
