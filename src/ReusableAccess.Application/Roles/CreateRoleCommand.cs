using MediatR;

namespace ReusableAccess.Application.Roles;

public sealed record CreateRoleCommand(
    string Code,
    string DisplayName)
    : IRequest<RoleCreationResult>;

public sealed record RoleCreationResult(
    bool AlreadyExists,
    Guid? RoleId,
    string? RoleCode,
    string? DisplayName);

public interface IRoleCommandStore
{
    Task<RoleCreationResult> CreateAsync(
        string code,
        string displayName,
        CancellationToken cancellationToken);
    Task<RoleRenameResult?> RenameAsync(
    Guid roleId,
    string displayName,
    CancellationToken cancellationToken);
}