using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ReusableAccess.Application.Roles;

// Read-only contract: orchestration and persistence remain outside this interface.
public interface IRoleQueries
{
    Task<IReadOnlyList<RoleListItem>> ListAsync(
        CancellationToken cancellationToken);
}
