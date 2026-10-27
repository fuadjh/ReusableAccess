using MediatR;

namespace ReusableAccess.Application.RolePermissions;

public sealed class GetRolePermissionsQueryHandler(
    IRolePermissionsQueryStore store)
    : IRequestHandler<
        GetRolePermissionsQuery,
        IReadOnlyList<RolePermissionOption>?>
{
    public async Task<IReadOnlyList<RolePermissionOption>?> Handle(
        GetRolePermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await store.GetAsync(
            request.RoleId,
            request.Search,
            cancellationToken);

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