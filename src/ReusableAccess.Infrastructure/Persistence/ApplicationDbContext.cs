using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence.Configurations;
namespace ReusableAccess.Infrastructure.Persistence;
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Role> Roles => Set<Role>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // نبود تغییر Schema باید با تست مدل تأیید شود، نه با حدس از روی نام فایل‌ها.
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new AuthenticationPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new AccountChallengeConfiguration());
        modelBuilder.ApplyConfiguration(new SecurityAuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new AccessScopeConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemConfiguration());
        modelBuilder.ApplyConfiguration(new UserAccessBundleConfiguration());
        modelBuilder.ApplyConfiguration(new UserAccessBundleRoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserAccessBundleScopeConfiguration());
    }
}
