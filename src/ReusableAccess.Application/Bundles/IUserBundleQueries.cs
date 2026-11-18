using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ReusableAccess.Application.Bundles;

// Read-only contract: orchestration and persistence remain outside this interface.
public interface IUserBundleQueries
{
    Task<IReadOnlyList<AdminBundleDetails>?> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
