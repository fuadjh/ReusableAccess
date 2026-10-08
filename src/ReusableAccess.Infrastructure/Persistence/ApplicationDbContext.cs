using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence.Configurations;
namespace ReusableAccess.Infrastructure.Persistence;

public sealed class ApplicationDbContext
    : IdentityUserContext<ApplicationUser, Guid>
{
   
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<Role> Roles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        //==============Configuration============================
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new AccessScopeConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleAssignmentConfiguration());
        //=======================================================
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.NationalId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(user => user.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(user => user.NationalId)
                .IsUnique();
        });
    }
}