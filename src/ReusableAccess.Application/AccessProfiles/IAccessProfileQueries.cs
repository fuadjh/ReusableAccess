using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ReusableAccess.Application.AccessProfiles;

// Read-only contract: orchestration and persistence remain outside this interface.
public interface IAccessProfileQueries
{
    Task<AccessProfilesSnapshot?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
