using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Infrastructure.Persistence;

internal static class PersistenceSaveChanges
{
    public static HashSet<object> CaptureTrackedEntities(
        ApplicationDbContext dbContext)
    {
        return dbContext.ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);
    }

    public static void DiscardFailedOperation(
        ApplicationDbContext dbContext,
        HashSet<object> trackedBeforeSave,
        object failedEntity)
    {
        var entries = dbContext.ChangeTracker.Entries().ToArray();

        foreach (var entry in entries)
        {
            var isFailedEntity =
                ReferenceEquals(entry.Entity, failedEntity);

            var wasAddedDuringSave =
                entry.State == EntityState.Added
                && !trackedBeforeSave.Contains(entry.Entity);

            if (isFailedEntity || wasAddedDuringSave)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    public static async Task SaveWithCodeConflictTranslationAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // این نگاشت برای یک درج مستقل است، نه یک واحد کار چندعملیاتی.
        var pending = dbContext.ChangeTracker
            .Entries()
            .Where(entry =>
                entry.State is EntityState.Added
                    or EntityState.Modified
                    or EntityState.Deleted)
            .ToArray();

        var trackedBeforeSave = CaptureTrackedEntities(dbContext);

        var independentInsert =
            dbContext.Database.CurrentTransaction is null
            && System.Transactions.Transaction.Current is null
            && pending.Length == 1
            && pending[0].State == EntityState.Added
            && pending[0].Entity is Role or AccessScope;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (
            independentInsert
            && IsSqlServerUniqueViolation(error)
            && error.Entries.Any(entry =>
                ReferenceEquals(entry.Entity, pending[0].Entity)))
        {
            // اگر Provider تراکنش فعال باقی گذاشته باشد، حدس نمی‌زنیم.
            if (dbContext.Database.CurrentTransaction is not null)
            {
                throw;
            }

            var entity = pending[0].Entity;

            CodeOwner owner;
            string code;
            bool conflictingCodeExists;

            switch (entity)
            {
                case Role role:
                    owner = CodeOwner.Role;
                    code = role.Code;

                    conflictingCodeExists = await dbContext.Roles.AnyAsync(
                        existing =>
                            existing.Code == role.Code
                            && existing.Id != role.Id,
                        cancellationToken);

                    break;

                case AccessScope scope:
                    owner = CodeOwner.Scope;
                    code = scope.Code;

                    conflictingCodeExists =
                        await dbContext.Set<AccessScope>().AnyAsync(
                            existing =>
                                existing.Code == scope.Code
                                && existing.Id != scope.Id,
                            cancellationToken);

                    break;

                default:
                    throw;
            }

            if (!conflictingCodeExists)
            {
                // مثلاً برخورد Primary Key؛ آن را Code تکراری نمی‌نامیم.
                throw;
            }

            DiscardFailedOperation(
                dbContext,
                trackedBeforeSave,
                entity);

            throw new DuplicateCodeException(owner, code, error);
        }
    }

    private static bool IsSqlServerUniqueViolation(
        DbUpdateException error)
    {
        return error.InnerException is SqlException sqlError
            && sqlError.Errors
                .Cast<SqlError>()
                .Any(item => item.Number is 2601 or 2627);
    }
}