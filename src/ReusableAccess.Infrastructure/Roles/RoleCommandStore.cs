using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Roles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Roles;

public sealed class RoleCommandStore(
    ApplicationDbContext dbContext) : IRoleCommandStore
{
    public async Task<RoleCreationResult> CreateAsync(
        string code,
        string displayName,
        CancellationToken cancellationToken)
    {
        var codeExists = await dbContext.Roles
            .AnyAsync(role => role.Code == code, cancellationToken);

        if (codeExists)
        {
            return new RoleCreationResult(
                AlreadyExists: true,
                RoleId: null,
                RoleCode: null,
                DisplayName: null);
        }

        var role = new Role(code, displayName);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RoleCreationResult(
            AlreadyExists: false,
            RoleId: role.Id,
            RoleCode: role.Code,
            DisplayName: role.DisplayName);
    }
    public async Task<RoleRenameResult?> RenameAsync(
    Guid roleId,
    string displayName,
    CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            item => item.Id == roleId,
            cancellationToken);

        if (role is null)
        {
            return null;
        }

        role.Rename(displayName);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RoleRenameResult(
            role.Id,
            role.Code,
            role.DisplayName);
    }
}