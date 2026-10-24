using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Scopes;

public sealed class ScopeCommandStore(
    ApplicationDbContext dbContext) : IScopeCommandStore
{
    public async Task<ScopeCreationResult> CreateAsync(
        CreateScopeCommand command,
        CancellationToken cancellationToken)
    {
        var codeExists = await dbContext.Set<AccessScope>()
            .AnyAsync(scope => scope.Code == command.Code, cancellationToken);

        if (codeExists)
        {
            return new ScopeCreationResult(
                ScopeCreationStatus.CodeConflict,
                Data: null);
        }

        if (command.ParentScopeId is Guid parentId)
        {
            var parentExists = await dbContext.Set<AccessScope>()
                .AnyAsync(scope => scope.Id == parentId, cancellationToken);

            if (!parentExists)
            {
                return new ScopeCreationResult(
                    ScopeCreationStatus.ParentNotFound,
                    Data: null);
            }
        }

        var scope = new AccessScope(
            command.Code,
            command.DisplayName,
            command.ParentScopeId);

        dbContext.Set<AccessScope>().Add(scope);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ScopeCreationResult(
            ScopeCreationStatus.Created,
            new ScopeCreatedDetails(
                scope.Id,
                scope.Code,
                scope.DisplayName,
                scope.ParentScopeId));
    }

    public async Task<ScopeRenameResult?> RenameAsync(
    Guid scopeId,
    string displayName,
    CancellationToken cancellationToken)
    {
        var scope = await dbContext.Set<AccessScope>()
            .SingleOrDefaultAsync(
                item => item.Id == scopeId,
                cancellationToken);

        if (scope is null)
        {
            return null;
        }

        scope.Rename(displayName);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ScopeRenameResult(
            scope.Id,
            scope.Code,
            scope.DisplayName,
            scope.ParentScopeId);
    }
}