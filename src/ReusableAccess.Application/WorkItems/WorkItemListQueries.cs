using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
namespace ReusableAccess.Application.WorkItems;
public interface IWorkItemListQueries { Task<IReadOnlyList<WorkItemRow>> ListAsync(Guid? scopeId, CancellationToken cancellationToken); }
public sealed record GetWorkItemListQuery(string ScopeMode, string? ScopeCode) : IRequest<IReadOnlyList<WorkItemRow>>, IConcurrencyConflictRequest;
public sealed class GetWorkItemListQueryHandler(IWorkItemListQueries queries, IWorkItemScopeQueries scopes)
    : IRequestHandler<GetWorkItemListQuery, IReadOnlyList<WorkItemRow>>
{
    public async Task<IReadOnlyList<WorkItemRow>> Handle(GetWorkItemListQuery request, CancellationToken cancellationToken)
    {
        var scope = await WorkItemScopeSelection.ResolveAsync(request.ScopeMode, request.ScopeCode, scopes, cancellationToken);
        return await queries.ListAsync(scope, cancellationToken);
    }
}
public sealed record GetWritableScopesQuery(string ScopeMode, string? ScopeCode) : IRequest<IReadOnlyList<ScopeRow>>, IConcurrencyConflictRequest;
public sealed class GetWritableScopesQueryHandler(IWorkItemScopeQueries scopes) : IRequestHandler<GetWritableScopesQuery, IReadOnlyList<ScopeRow>>
{
    public async Task<IReadOnlyList<ScopeRow>> Handle(GetWritableScopesQuery request, CancellationToken cancellationToken)
    {
        var scope = await WorkItemScopeSelection.ResolveAsync(request.ScopeMode, request.ScopeCode, scopes, cancellationToken);
        return await scopes.ListAsync(scope, cancellationToken);
    }
}
