using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.RolePermissions;

public sealed class RolePermissionsQueryStore(
    ApplicationDbContext dbContext) : IRolePermissionsQueryStore
{
    public async Task<RolePermissionsReadModel?> GetAsync(
        Guid roleId,
        string? search,
        CancellationToken cancellationToken)
    {
        var roleExists = await dbContext.Set<Role>()
            .AsNoTracking()
            .AnyAsync(role => role.Id == roleId, cancellationToken);

        if (!roleExists)
        {
            return null;
        }

        var assignedPermissionIds = await dbContext.Set<RolePermission>()
            .AsNoTracking()
            .Where(item => item.RoleId == roleId)
            .Select(item => item.PermissionId)
            .ToListAsync(cancellationToken);

        var assignedIds = assignedPermissionIds.ToHashSet();

        var permissionQuery = dbContext.Set<Permission>()
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            permissionQuery = permissionQuery.Where(permission =>
                permission.Key.Contains(search)
                || permission.Description.Contains(search));
        }

        var permissions = await permissionQuery
            .OrderBy(permission => permission.Key)
            .Select(permission => new
            {
                permission.Id,
                permission.Key,
                permission.Description
            })
            .ToListAsync(cancellationToken);

        return new RolePermissionsReadModel(
            permissions.Select(permission =>
                new RolePermissionReadModel(
                    permission.Id,
                    permission.Key,
                    permission.Description,
                    assignedIds.Contains(permission.Id)))
                .ToArray());
    }
}