using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Accounts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.Accounts;
public sealed class AccountRepository(ApplicationDbContext db) : IAccountRepository
{
    public Task<AuthenticationPolicy> ReadPolicyAsync(CancellationToken cancellationToken)
        => db.Set<AuthenticationPolicy>().AsNoTracking().SingleAsync(p => p.Id == 1, cancellationToken);
    public Task<AuthenticationPolicy> ReadPolicyForUpdateAsync(CancellationToken cancellationToken)
        => db.Set<AuthenticationPolicy>().AsTracking().SingleAsync(p => p.Id == 1, cancellationToken);
    public async Task<AccountRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var u = await db.Users.AsTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return u is null ? null : AccountIdentityMapping.Read(u);
    }
    public async Task<AccountRecord?> FindActiveByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var u = await db.Users.AsTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        return u is null ? null : AccountIdentityMapping.Read(u);
    }
    public async Task<AccountRecord?> FindByNationalIdAsync(string nationalId, CancellationToken cancellationToken)
    {
        var u = await db.Users.AsTracking().SingleOrDefaultAsync(x => x.NationalId == nationalId, cancellationToken);
        return u is null ? null : AccountIdentityMapping.Read(u);
    }
    public Task<bool> NationalIdExistsAsync(string nationalId, Guid? excludingUserId, CancellationToken cancellationToken)
        => db.Users.AnyAsync(x => x.NationalId == nationalId && (!excludingUserId.HasValue || x.Id != excludingUserId.Value), cancellationToken);
    public Task<bool> HasOtherActiveAdministratorAsync(Guid excludingUserId, CancellationToken cancellationToken)
        => db.Users.AnyAsync(x => x.Id != excludingUserId && x.IsActive && x.IsPlatformSuperAdmin, cancellationToken);
    public Task<bool> HasRecentChallengeAsync(Guid userId, string purpose, DateTimeOffset cutoff, CancellationToken cancellationToken)
        => db.Set<AccountChallenge>().AnyAsync(x => x.UserId == userId && x.Purpose == purpose && x.CreatedUtc > cutoff, cancellationToken);
    public Task<AccountChallenge?> FindChallengeAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<AccountChallenge>().AsTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public void AddChallenge(AccountChallenge challenge) => db.Add(challenge);
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => _ = await db.SaveChangesAsync(cancellationToken);
}
