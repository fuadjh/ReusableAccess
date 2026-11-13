using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Application.RolePermissions;

public interface IRolePermissionsRepository
{
    Task<Role?> FindRoleForUpdateAsync(
        Guid roleId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Permission>> FindPermissionsByKeysAsync(
        string[] keys,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}