using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed class GetScopesQueryHandler(IScopeReadStore readStore)
    : IRequestHandler<GetScopesQuery, IReadOnlyList<ScopeListItem>>
{
    public Task<IReadOnlyList<ScopeListItem>> Handle(
        GetScopesQuery request,
        CancellationToken cancellationToken)
    {
        return readStore.ListAsync(cancellationToken);
    }
}