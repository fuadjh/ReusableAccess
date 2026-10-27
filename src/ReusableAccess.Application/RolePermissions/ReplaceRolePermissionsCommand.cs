using MediatR;

namespace ReusableAccess.Application.RolePermissions;

public sealed record ReplaceRolePermissionsCommand(
    Guid RoleId,
    string[]? PermissionKeys)
    : IRequest<RolePermissionsReplacementResult>;

public enum RolePermissionsReplacementStatus
{
    Updated,
    InvalidRequest,
    RoleNotFound,
    UnknownPermissions
}

public sealed record RolePermissionsReplacementResult(
    RolePermissionsReplacementStatus Status,
    Guid RoleId,
    string[] PermissionKeys,
    string[] UnknownPermissionKeys);

public interface IRolePermissionsCommandStore
{
    Task<RolePermissionsReplacementResult> ReplaceAsync(
        ReplaceRolePermissionsCommand command,
        CancellationToken cancellationToken);
}