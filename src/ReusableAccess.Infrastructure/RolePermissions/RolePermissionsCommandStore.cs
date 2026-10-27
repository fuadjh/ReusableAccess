using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.RolePermissions;

public sealed class RolePermissionsCommandStore(
    ApplicationDbContext dbContext) : IRolePermissionsCommandStore
{
    public async Task<RolePermissionsReplacementResult> ReplaceAsync(
        ReplaceRolePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        // این اعتبارسنجی باید قبل از بررسی وجود Role باشد،
        // تا ترتیب پاسخ‌های HTTP فعلی حفظ شود.
        if (command.PermissionKeys is null
            || command.PermissionKeys.Any(string.IsNullOrWhiteSpace))
        {
            return new RolePermissionsReplacementResult(
                RolePermissionsReplacementStatus.InvalidRequest,
                command.RoleId,
                [],
                []);
        }

        var requestedKeys = command.PermissionKeys
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var role = await dbContext.Set<Role>()
            .Include(item => item.Permissions)
            .SingleOrDefaultAsync(
                item => item.Id == command.RoleId,
                cancellationToken);

        if (role is null)
        {
            return new RolePermissionsReplacementResult(
                RolePermissionsReplacementStatus.RoleNotFound,
                command.RoleId,
                [],
                []);
        }

        var permissions = await dbContext.Set<Permission>()
            .Where(permission => requestedKeys.Contains(permission.Key))
            .ToListAsync(cancellationToken);

        var permissionsByKey = permissions.ToDictionary(
            permission => permission.Key,
            StringComparer.Ordinal);

        var unknownKeys = requestedKeys
            .Where(key => !permissionsByKey.ContainsKey(key))
            .ToArray();

        if (unknownKeys.Length > 0)
        {
            return new RolePermissionsReplacementResult(
                RolePermissionsReplacementStatus.UnknownPermissions,
                role.Id,
                [],
                unknownKeys);
        }

        var desiredPermissionIds = permissions
            .Select(permission => permission.Id)
            .ToHashSet();

        foreach (var currentPermission in role.Permissions.ToArray())
        {
            if (!desiredPermissionIds.Contains(
                    currentPermission.PermissionId))
            {
                role.RemovePermission(currentPermission.PermissionId);
            }
        }

        foreach (var permission in permissions)
        {
            role.AddPermission(permission);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var permissionKeys = permissions
            .Select(permission => permission.Key)
            .OrderBy(key => key)
            .ToArray();

        return new RolePermissionsReplacementResult(
            RolePermissionsReplacementStatus.Updated,
            role.Id,
            permissionKeys,
            []);
    }
}