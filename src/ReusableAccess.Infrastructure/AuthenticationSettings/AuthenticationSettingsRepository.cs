using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.AuthenticationSettings;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.AuthenticationSettings;
public sealed class AuthenticationSettingsRepository(ApplicationDbContext db) : IAuthenticationSettingsRepository
{
    public Task<AccountVerification?> FindAccountAsync(Guid userId, CancellationToken cancellationToken)
        => db.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new AccountVerification(user.EmailConfirmed, user.PhoneNumberConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
    public Task<AuthenticationPolicy> ReadAsync(bool forUpdate, CancellationToken cancellationToken)
    {
        var query = db.Set<AuthenticationPolicy>().AsQueryable();
        return (forUpdate ? query.AsTracking() : query.AsNoTracking())
            .SingleAsync(p => p.Id == 1, cancellationToken);
    }
    public async Task InvalidateAllSessionsAsync(CancellationToken cancellationToken)
        => _ = await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.SessionVersion,
            u => u.SessionVersion + 1), cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => _ = await db.SaveChangesAsync(cancellationToken);
}
