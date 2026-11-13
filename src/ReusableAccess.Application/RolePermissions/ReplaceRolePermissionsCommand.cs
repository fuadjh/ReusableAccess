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

public sealed class ReplaceRolePermissionsCommandHandler(
    IRolePermissionsRepository repository)
    : IRequestHandler<
        ReplaceRolePermissionsCommand,
        RolePermissionsReplacementResult>
{
    public async Task<RolePermissionsReplacementResult> Handle(
        ReplaceRolePermissionsCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.PermissionKeys is null
            || request.PermissionKeys.Any(string.IsNullOrWhiteSpace))
        {
            return new RolePermissionsReplacementResult(
                RolePermissionsReplacementStatus.InvalidRequest,
                request.RoleId,
                [],
                []);
        }

        var requestedKeys = request.PermissionKeys
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var role = await repository.FindRoleForUpdateAsync(
            request.RoleId,
            cancellationToken);

        if (role is null)
        {
            return new RolePermissionsReplacementResult(
                RolePermissionsReplacementStatus.RoleNotFound,
                request.RoleId,
                [],
                []);
        }

        var permissions = await repository.FindPermissionsByKeysAsync(
            requestedKeys,
            cancellationToken);

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

        // Snapshot لازم است چون مجموعه در طول حلقه تغییر می‌کند.
        foreach (var current in role.Permissions.ToArray())
        {
            if (!desiredPermissionIds.Contains(current.PermissionId))
            {
                role.RemovePermission(current.PermissionId);
            }
        }

        foreach (var permission in permissions)
        {
            role.AddPermission(permission);
        }

        await repository.SaveChangesAsync(cancellationToken);

        // ترتیب خروجی فعلی حفظ می‌شود.
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