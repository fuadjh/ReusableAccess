using ReusableAccess.Application.Persistence;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using Xunit;
using Assert = Xunit.Assert;

namespace ReusableAccess.Application.Tests.Scopes;

public sealed class ScopeDeletionCommandTests
{
    [Fact]
    public async Task Successful_delete_commits_after_save()
    {
        var fixture = new Fixture();

        var result = await fixture.HandleAsync();

        Assert.Equal(ScopeDeletionStatus.Deleted, result);

        Assert.Equal(
            new[] { "begin", "find", "dependencies", "delete", "commit", "dispose" },
            fixture.Events.ToArray());
    }

    [Fact]
    public async Task Missing_scope_does_not_delete_or_commit()
    {
        var fixture = new Fixture { MissingScope = true };

        var result = await fixture.HandleAsync();

        Assert.Equal(ScopeDeletionStatus.NotFound, result);

        Assert.Equal(
            new[] { "begin", "find", "dispose" },
            fixture.Events.ToArray());
    }

    [Fact]
    public async Task Dependencies_prevent_delete_and_commit()
    {
        var fixture = new Fixture { HasDependencies = true };

        var result = await fixture.HandleAsync();

        Assert.Equal(ScopeDeletionStatus.HasDependencies, result);

        Assert.Equal(
            new[] { "begin", "find", "dependencies", "dispose" },
            fixture.Events.ToArray());
    }

    [Fact]
    public async Task Concurrency_conflict_rolls_back()
    {
        var fixture = new Fixture { DeleteSucceeds = false };

        var result = await fixture.HandleAsync();

        Assert.Equal(ScopeDeletionStatus.ConcurrencyConflict, result);

        Assert.Equal(
            new[]
            {
                "begin", "find", "dependencies",
                "delete", "rollback", "dispose"
            },
            fixture.Events.ToArray());
    }

    [Fact]
    public async Task Unknown_failure_is_not_hidden_and_transaction_is_disposed()
    {
        var failure = new InvalidOperationException("Database unavailable");

        var fixture = new Fixture { DeleteFailure = failure };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => { await fixture.HandleAsync(); });

        Assert.Same(failure, actual);
        Assert.DoesNotContain("commit", fixture.Events);
        Assert.Equal("dispose", fixture.Events.Last());
    }

    [Fact]
    public async Task Already_cancelled_request_does_not_begin_transaction()
    {
        var fixture = new Fixture();

        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => { await fixture.HandleAsync(source.Token); });

        Assert.Empty(fixture.Events);
    }

    private sealed class Fixture :
        IScopeDeletionRepository,
        IWriteTransactionFactory,
        IWriteTransaction
    {
        private readonly AccessScope _scope = new("clinic", "Clinic");

        public List<string> Events { get; } = [];

        public bool MissingScope { get; init; }

        public bool HasDependencies { get; init; }

        public bool DeleteSucceeds { get; init; } = true;

        public Exception? DeleteFailure { get; init; }

        public Task<ScopeDeletionStatus> HandleAsync(
            CancellationToken cancellationToken = default)
        {
            return new DeleteScopeCommandHandler(this, this).Handle(
                new DeleteScopeCommand(_scope.Id),
                cancellationToken);
        }

        public Task<IWriteTransaction> BeginSerializableAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("begin");

            return Task.FromResult<IWriteTransaction>(this);
        }

        public Task<AccessScope?> FindForUpdateAsync(
            Guid scopeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("find");

            return Task.FromResult<AccessScope?>(
                MissingScope ? null : _scope);
        }

        public Task<bool> HasDependenciesAsync(
            Guid scopeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("dependencies");

            return Task.FromResult(HasDependencies);
        }

        public Task<bool> TryDeleteAsync(
            AccessScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("delete");

            return DeleteFailure is null
                ? Task.FromResult(DeleteSucceeds)
                : Task.FromException<bool>(DeleteFailure);
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("commit");
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("rollback");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Events.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }
}