using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReusableAccess.Application.RolePermissions;

public interface IRolePermissionsQueries
{
    Task<RolePermissionsReadModel?> GetAsync(
        Guid roleId,
        string? search,
        CancellationToken cancellationToken);
}