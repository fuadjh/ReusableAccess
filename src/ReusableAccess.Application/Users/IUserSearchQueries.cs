using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ReusableAccess.Application.Users;

// Read-only contract: orchestration and persistence remain outside this interface.
public interface IUserSearchQueries
{
    Task<IReadOnlyList<AdminUserSearchResult>> SearchAsync(
        string search,
        CancellationToken cancellationToken);
}
