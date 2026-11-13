using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Roles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Roles;

public sealed class RoleRepository(
    ApplicationDbContext dbContext)
    : IRoleRepository
{
    public Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return dbContext.Roles.AnyAsync(
            role => role.Code == code,
            cancellationToken);
    }

    public Task<Role?> FindForUpdateAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        // این موجودیت برای تغییر خوانده می‌شود.
        // AsTracking قرارداد متد را صریح می‌کند،
        // حتی اگر تنظیم پیش‌فرض Context بعداً تغییر کند.
        return dbContext.Roles
            .AsTracking()
            .SingleOrDefaultAsync(
                role => role.Id == roleId,
                cancellationToken);
    }

    public void Add(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        dbContext.Roles.Add(role);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}