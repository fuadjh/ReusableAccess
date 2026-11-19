using MediatR;
using ReusableAccess.Domain.Enums;

namespace ReusableAccess.Application.WorkItems;

public sealed class GetWorkItemsQueryHandler(
    IWorkItemScopeQueries scopes,
    IWorkItemListQueries items)
    : IRequestHandler<GetWorkItemsQuery, IReadOnlyList<WorkItemSummary>?>
{
    public async Task<IReadOnlyList<WorkItemSummary>?> Handle(
        GetWorkItemsQuery request,
        CancellationToken cancellationToken)
    {
        Guid? scopeId;

        switch (request.ScopeMode)
        {
            case ScopeSelectionMode.Selected:
                scopeId = await scopes.FindIdByCodeAsync(
                    request.ScopeCode, cancellationToken);

                if (scopeId is null)
                {
                    // قرارداد مسیر عمومی: endpoint این نتیجه را به Forbid تبدیل می‌کند.
                    return null;
                }

                break;

            case ScopeSelectionMode.All:
                // Policy پیش از اجرای Query دسترسی All را بررسی می‌کند.
                scopeId = null;
                break;

            default:
                // حالت ناشناخته نباید به خواندن بدون فیلتر تبدیل شود.
                return null;
        }

        var rows = await items.ListAsync(scopeId, cancellationToken);

        return rows
            .Select(row => new WorkItemSummary(row.Id, row.Title, row.Status))
            .ToArray();
    }
}