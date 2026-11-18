using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReusableAccess.Domain.Enums;

namespace ReusableAccess.Application.WorkItems;

// Read-only contract: orchestration and persistence remain outside this interface.
public interface IWorkItemsQueries
{
    Task<IReadOnlyList<WorkItemSummary>?> GetAsync(
     ScopeSelectionMode scopeMode,
     string? scopeCode,
     CancellationToken cancellationToken);
}
