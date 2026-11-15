using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Roles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.Roles;
public sealed class RoleDeletionRepository(ApplicationDbContext db) : IRoleDeletionRepository
{
    public Task<Role?> FindForUpdateAsync(Guid roleId, CancellationToken cancellationToken)
        => db.Roles.AsTracking().SingleOrDefaultAsync(role => role.Id == roleId, cancellationToken);
    public async Task<bool> HasAssignmentsAsync(Guid roleId, CancellationToken cancellationToken)
        => await db.Set<UserRoleAssignment>().AnyAsync(x => x.RoleId == roleId, cancellationToken)
           || await db.Set<UserAccessBundleRole>().AnyAsync(x => x.RoleId == roleId, cancellationToken);
    public async Task RemoveWithPermissionsAsync(Role role, CancellationToken cancellationToken)
    {
        db.RemoveRange(await db.Set<RolePermission>().Where(x => x.RoleId == role.Id).ToListAsync(cancellationToken));
        db.Remove(role);
    }
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => _ = await db.SaveChangesAsync(cancellationToken);
}
