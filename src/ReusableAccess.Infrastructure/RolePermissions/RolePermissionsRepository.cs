using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.RolePermissions;

public sealed class RolePermissionsRepository(
    ApplicationDbContext dbContext)
    : IRolePermissionsRepository
{
    public Task<Role?> FindRoleForUpdateAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<Role>()
            .AsTracking()
            .Include(role => role.Permissions)
            .SingleOrDefaultAsync(
                role => role.Id == roleId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Permission>> FindPermissionsByKeysAsync(
        string[] keys,
        CancellationToken cancellationToken)
    {
        // Permission در این عملیات فقط خوانده می‌شود.
        return await dbContext.Set<Permission>()
            .AsNoTracking()
            .Where(permission => keys.Contains(permission.Key))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}