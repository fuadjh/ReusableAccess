using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Users;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Users;

public sealed class UserSearchStore(
    ApplicationDbContext dbContext) : IUserSearchStore
{
    public async Task<IReadOnlyList<AdminUserSearchResult>> SearchAsync(
        string search,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                (user.UserName != null && user.UserName.Contains(search))
                || user.DisplayName.Contains(search))
            .OrderBy(user => user.DisplayName)
            .Take(50)
            .Select(user => new AdminUserSearchResult(
                user.Id,
                user.UserName,
                user.DisplayName))
            .ToListAsync(cancellationToken);
    }
}