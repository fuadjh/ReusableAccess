using MediatR;

namespace ReusableAccess.Application.Roles;

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleListItem>>;

public sealed record RoleListItem(
    Guid RoleId,
    string RoleCode,
    string DisplayName);

public interface IRoleReadStore
{
    Task<IReadOnlyList<RoleListItem>> ListAsync(
        CancellationToken cancellationToken);
}