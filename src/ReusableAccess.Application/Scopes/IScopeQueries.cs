using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ReusableAccess.Application.Scopes;

// Read-only contract: orchestration and persistence remain outside this interface.
public interface IScopeQueries
{
    Task<IReadOnlyList<ScopeListItem>> ListAsync(
        CancellationToken cancellationToken);
}
