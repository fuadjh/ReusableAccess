namespace ReusableAccess.Application.Persistence;

public interface IWriteTransactionFactory
{
    Task<IWriteTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken);
}

public interface IWriteTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}