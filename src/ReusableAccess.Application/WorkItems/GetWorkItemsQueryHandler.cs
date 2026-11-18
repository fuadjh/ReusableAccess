using MediatR;

namespace ReusableAccess.Application.WorkItems;

public sealed class GetWorkItemsQueryHandler(
    IWorkItemsQueries store)
    : IRequestHandler<
        GetWorkItemsQuery,
        IReadOnlyList<WorkItemSummary>?>
{
    public Task<IReadOnlyList<WorkItemSummary>?> Handle(
        GetWorkItemsQuery request,
        CancellationToken cancellationToken)
    {
        return store.GetAsync(
            request.ScopeMode,
            request.ScopeCode,
            cancellationToken);
    }
}