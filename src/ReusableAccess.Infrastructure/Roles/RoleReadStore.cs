using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Roles;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Roles;

public sealed class RoleReadStore(
    ApplicationDbContext dbContext) : IRoleReadStore
{
    public async Task<IReadOnlyList<RoleListItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Code)
            .Select(role => new RoleListItem(
                role.Id,
                role.Code,
                role.DisplayName))
            .ToListAsync(cancellationToken);
    }
}