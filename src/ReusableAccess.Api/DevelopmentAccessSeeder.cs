using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api;

public static class DevelopmentAccessSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        Guid userId)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        const string permissionKey = "diagnostics.read";

        var permission = await db.Set<Permission>()
            .SingleOrDefaultAsync(item => item.Key == permissionKey);

        if (permission is null)
        {
            permission = new Permission(
                permissionKey,
                "دسترسی آزمایشی به endpoint تشخیصی");

            db.Set<Permission>().Add(permission);
        }

        var clinicReaderRole = await GetOrCreateRoleAsync(
            db,
            "DIAGNOSTICS_READER",
            "Diagnostics reader");

        var hseReaderRole = await GetOrCreateRoleAsync(
            db,
            "HSE_READER",
            "HSE reader");

        var clinic1 = await GetOrCreateScopeAsync(
            db,
            "CLINIC_1",
            "درمانگاه ۱");

        var hse = await GetOrCreateScopeAsync(
            db,
            "HSE",
            "واحد HSE");

        // ذخیرهٔ نقش‌ها، محدوده‌ها و مجوز پیش از ساخت روابط آن‌ها.
        await db.SaveChangesAsync();

        clinicReaderRole.AddPermission(permission);
        hseReaderRole.AddPermission(permission);

        await db.SaveChangesAsync();

        // هر پروفایل تخصیص دقیق و مستقل دارد؛ ارث‌بری بین محدوده‌ها نداریم.
        await EnsureAssignmentAsync(
            db,
            userId,
            clinicReaderRole.Id,
            clinic1.Id);

        await EnsureAssignmentAsync(
            db,
            userId,
            hseReaderRole.Id,
            hse.Id);

        await db.SaveChangesAsync();
    }

    private static async Task<Role> GetOrCreateRoleAsync(
        ApplicationDbContext db,
        string roleCode,
        string displayName)
    {
        var role = await db.Set<Role>()
            .Include(item => item.Permissions)
            .SingleOrDefaultAsync(item => item.Code == roleCode);

        if (role is not null)
        {
            return role;
        }

        role = new Role(roleCode, displayName);
        db.Set<Role>().Add(role);

        return role;
    }

    private static async Task<AccessScope> GetOrCreateScopeAsync(
        ApplicationDbContext db,
        string scopeCode,
        string displayName)
    {
        var scope = await db.Set<AccessScope>()
            .SingleOrDefaultAsync(item => item.Code == scopeCode);

        if (scope is not null)
        {
            return scope;
        }

        scope = new AccessScope(scopeCode, displayName);
        db.Set<AccessScope>().Add(scope);

        return scope;
    }

    private static async Task EnsureAssignmentAsync(
        ApplicationDbContext db,
        Guid userId,
        Guid roleId,
        Guid scopeId)
    {
        var assignment = await db.Set<UserRoleAssignment>()
            .SingleOrDefaultAsync(item =>
                item.UserId == userId &&
                item.RoleId == roleId &&
                item.ScopeId == scopeId);

        if (assignment is null)
        {
            db.Set<UserRoleAssignment>().Add(
                new UserRoleAssignment(userId, roleId, scopeId));

            return;
        }

        assignment.Enable();
    }
}