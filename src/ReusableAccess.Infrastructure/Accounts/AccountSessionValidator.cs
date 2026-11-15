using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Accounts;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.Accounts;
public sealed class AccountSessionValidator(ApplicationDbContext db) : IAccountSessionValidator
{
    public Task<bool> IsCurrentAsync(Guid userId, int sessionVersion, CancellationToken cancellationToken)
        => db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive && x.SessionVersion == sessionVersion, cancellationToken);
}
