using MediatR;

namespace ReusableAccess.Application.WorkItems;

public sealed class GetWorkItemsQueryHandler(
    IWorkItemsQueryStore store)
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