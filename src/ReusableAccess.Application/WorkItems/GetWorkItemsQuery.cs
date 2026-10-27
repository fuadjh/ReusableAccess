using MediatR;
using ReusableAccess.Domain.Enums;
namespace ReusableAccess.Application.WorkItems;

public sealed record GetWorkItemsQuery(
    ScopeSelectionMode ScopeMode,
    string? ScopeCode)
    : IRequest<IReadOnlyList<WorkItemSummary>?>;


public sealed record WorkItemSummary(
    Guid Id,
    string Title,
    string Status);

public interface IWorkItemsQueryStore
{
    Task<IReadOnlyList<WorkItemSummary>?> GetAsync(
     ScopeSelectionMode scopeMode,
     string? scopeCode,
     CancellationToken cancellationToken);
}