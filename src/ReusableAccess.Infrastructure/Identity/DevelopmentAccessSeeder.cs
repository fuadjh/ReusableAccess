using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Seeding;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Identity;

public sealed class DevelopmentAccessSeeder(
    ApplicationDbContext db)
    : IDevelopmentAccessSeeder
{
    public async Task SeedAsync(Guid userId)
    {
        const string permissionKey = "diagnostics.read";
        const string workItemsPermissionKey = "workitems.read";

        var workItemsPermission = await db.Set<Permission>()
            .SingleOrDefaultAsync(
                item => item.Key == workItemsPermissionKey);

        if (workItemsPermission is null)
        {
            workItemsPermission = new Permission(
                workItemsPermissionKey,
                "خواندن موارد کاری در محدودهٔ مجاز");

            db.Set<Permission>().Add(workItemsPermission);
        }

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
            "DIAGNOSTICS_READER",
            "Diagnostics reader");

        var hseReaderRole = await GetOrCreateRoleAsync(
            "HSE_READER",
            "HSE reader");

        var clinic1 = await GetOrCreateScopeAsync(
            "CLINIC_1",
            "درمانگاه ۱");

        var hse = await GetOrCreateScopeAsync(
            "HSE",
            "واحد HSE");

        // نقش‌ها، محدوده‌ها و مجوزها باید پیش از ساخت روابط ذخیره شوند.
        await db.SaveChangesAsync();

        clinicReaderRole.AddPermission(permission);
        hseReaderRole.AddPermission(permission);
        clinicReaderRole.AddPermission(workItemsPermission);
        hseReaderRole.AddPermission(workItemsPermission);

        await db.SaveChangesAsync();

        // هر تخصیص، محدودهٔ مستقل دارد؛ محدوده‌ها از یکدیگر ارث نمی‌برند.
        await EnsureBundleAsync(
            userId,
            "Clinic reader — Clinic 1",
            clinicReaderRole.Id,
            clinic1.Id,
            sortOrder: 0);

        await EnsureBundleAsync(
            userId,
            "HSE reader — HSE",
            hseReaderRole.Id,
            hse.Id,
            sortOrder: 1);

        await EnsureWorkItemAsync(
            clinic1.Id,
            "پیگیری معاینه دوره‌ای",
            "Open");

        await EnsureWorkItemAsync(
            hse.Id,
            "بازرسی تجهیزات ایمنی",
            "Open");

        await db.SaveChangesAsync();
    }

    private async Task<Role> GetOrCreateRoleAsync(
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

    private async Task<AccessScope> GetOrCreateScopeAsync(
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

    private async Task EnsureBundleAsync(
        Guid userId,
        string bundleName,
        Guid roleId,
        Guid scopeId,
        int sortOrder)
    {
        var alreadyRepresented = await db.Set<UserAccessBundle>()
            .Where(bundle =>
                bundle.UserId == userId &&
                bundle.ScopeSelectionMode == ScopeSelectionMode.Selected)
            .AnyAsync(bundle =>
                bundle.Roles.Any(role => role.RoleId == roleId) &&
                bundle.Scopes.Any(scope => scope.ScopeId == scopeId));

        if (alreadyRepresented)
        {
            return;
        }

        db.Set<UserAccessBundle>().Add(
            new UserAccessBundle(
                userId,
                bundleName,
                [roleId],
                ScopeSelectionMode.Selected,
                [scopeId],
                sortOrder));
    }

    private async Task EnsureWorkItemAsync(
        Guid scopeId,
        string title,
        string status)
    {
        var exists = await db.Set<WorkItem>()
            .AnyAsync(item =>
                item.ScopeId == scopeId &&
                item.Title == title);

        if (exists)
        {
            return;
        }

        db.Set<WorkItem>().Add(
            new WorkItem(scopeId, title, status));
    }
}