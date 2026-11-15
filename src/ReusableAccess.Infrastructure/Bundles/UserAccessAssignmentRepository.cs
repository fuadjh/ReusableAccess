using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.Bundles;
public sealed class UserAccessAssignmentRepository(ApplicationDbContext db) : IUserAccessAssignmentRepository
{
    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
        => db.Users.AnyAsync(x => x.Id == userId, cancellationToken);
    public Task<Role?> FindRoleAsync(Guid roleId, CancellationToken cancellationToken)
        => db.Roles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == roleId, cancellationToken);
    public Task<AccessScope?> FindScopeAsync(Guid scopeId, CancellationToken cancellationToken)
        => db.Set<AccessScope>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == scopeId, cancellationToken);
    public Task<bool> HasBundlesAsync(Guid userId, CancellationToken cancellationToken)
        => db.Set<UserAccessBundle>().AnyAsync(x => x.UserId == userId, cancellationToken);
    public async Task<IReadOnlyList<UserRoleAssignment>> LoadEnabledLegacyAssignmentsAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Set<UserRoleAssignment>().AsNoTracking().Where(x => x.UserId == userId && x.IsEnabled).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<UserAccessBundle>> LoadBundlesForUpdateAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Set<UserAccessBundle>().AsTracking().Include(x => x.Roles).Include(x => x.Scopes)
            .Where(x => x.UserId == userId).ToListAsync(cancellationToken);
    public void Add(UserAccessBundle bundle) => db.Add(bundle);
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => _ = await db.SaveChangesAsync(cancellationToken);
}
