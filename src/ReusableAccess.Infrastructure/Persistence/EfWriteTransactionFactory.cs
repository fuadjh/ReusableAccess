using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ReusableAccess.Application.Persistence;

namespace ReusableAccess.Infrastructure.Persistence;

public sealed class EfWriteTransactionFactory(
    ApplicationDbContext dbContext)
    : IWriteTransactionFactory
{
    public async Task<IWriteTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (dbContext.Database.CurrentTransaction is not null
            || System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException(
                "An existing transaction cannot be replaced.");
        }

        // مرز تراکنش باید پیش از تغییر وضعیت شروع شود.
        if (dbContext.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException(
                "Start the transaction before changing tracked entities.");
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        return new EfWriteTransaction(transaction);
    }

    private sealed class EfWriteTransaction(
        IDbContextTransaction transaction)
        : IWriteTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            return transaction.CommitAsync(cancellationToken);
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            return transaction.RollbackAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            // تراکنش Commitنشده هنگام Dispose بازگردانده می‌شود.
            return transaction.DisposeAsync();
        }
    }
}