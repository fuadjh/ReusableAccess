using MediatR;

namespace ReusableAccess.Application.Roles;

public sealed record RenameRoleCommand(
    Guid RoleId,
    string DisplayName)
    : IRequest<RoleRenameResult?>;

public sealed record RoleRenameResult(
    Guid RoleId,
    string RoleCode,
    string DisplayName);