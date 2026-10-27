using MediatR;

namespace ReusableAccess.Application.RolePermissions;

public sealed record GetRolePermissionsQuery(
    Guid RoleId,
    string? Search)
    : IRequest<IReadOnlyList<RolePermissionOption>?>;

public sealed record RolePermissionOption(
    Guid PermissionId,
    string Key,
    string Description,
    string Category,
    bool IsSelected);

public sealed record RolePermissionsReadModel(
    IReadOnlyList<RolePermissionReadModel> Permissions);

public sealed record RolePermissionReadModel(
    Guid PermissionId,
    string Key,
    string Description,
    bool IsSelected);

public interface IRolePermissionsQueryStore
{
    Task<RolePermissionsReadModel?> GetAsync(
        Guid roleId,
        string? search,
        CancellationToken cancellationToken);
}