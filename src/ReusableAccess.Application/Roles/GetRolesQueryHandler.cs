using MediatR;

namespace ReusableAccess.Application.Roles;

public sealed class GetRolesQueryHandler(IRoleReadStore readStore)
    : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleListItem>>
{
    public Task<IReadOnlyList<RoleListItem>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        return readStore.ListAsync(cancellationToken);
    }
}