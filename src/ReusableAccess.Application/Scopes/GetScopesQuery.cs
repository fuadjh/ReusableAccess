using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed record GetScopesQuery : IRequest<IReadOnlyList<ScopeListItem>>;

public sealed record ScopeListItem(
    Guid ScopeId,
    string ScopeCode,
    string DisplayName,
    Guid? ParentScopeId);

public interface IScopeReadStore
{
    Task<IReadOnlyList<ScopeListItem>> ListAsync(
        CancellationToken cancellationToken);
}