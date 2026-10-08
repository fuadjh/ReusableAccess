using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReusableAccess.Domain.Entities;
// اگر namespace کلاس ApplicationUser در پروژه‌ات متفاوت است، این using را مطابق همان namespace تنظیم کن.
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Persistence.Configurations;

public sealed class UserRoleAssignmentConfiguration
    : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("UserRoleAssignments");

        builder.HasKey(assignment => assignment.Id);

        builder.HasIndex(assignment => new
        {
            assignment.UserId,
            assignment.RoleId,
            assignment.ScopeId
        })
            .IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccessScope>()
            .WithMany()
            .HasForeignKey(assignment => assignment.ScopeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}