using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace ReusableAccess.Application.RolePermissions;

public sealed class GetRolePermissionsQueryHandler(
    IRolePermissionsQueries queries)
    : IRequestHandler<
        GetRolePermissionsQuery,
        IReadOnlyList<RolePermissionOption>?>
{
    public async Task<IReadOnlyList<RolePermissionOption>?> Handle(
        GetRolePermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetAsync(
            request.RoleId,
            request.Search,
            cancellationToken);

        // رفتار قبلی برای Role ناموجود حفظ می‌شود.
        if (result is null)
        {
            return null;
        }

        return result.Permissions
            .Select(permission =>
            {
                var separator = permission.Key.IndexOf('.');

                var category = separator > 0
                    ? permission.Key[..separator]
                    : "general";

                return new RolePermissionOption(
                    permission.PermissionId,
                    permission.Key,
                    permission.Description,
                    category,
                    permission.IsSelected);
            })
            .ToArray();
    }
}