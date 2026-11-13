using ReusableAccess.Application.Persistence;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using Xunit;
using Assert = Xunit.Assert;

namespace ReusableAccess.Application.Tests.Scopes;

public sealed class ScopeCommandTests
{
    [Fact]
    public async Task Create_checks_normalized_code_and_saves_once()
    {
        var repository = new FakeRepository();
        var parentId = Guid.NewGuid();

        var result = await new CreateScopeCommandHandler(repository)
            .Handle(
                new CreateScopeCommand(" clinic ", " Clinic ", parentId),
                CancellationToken.None);

        Assert.Equal(ScopeCreationStatus.Created, result.Status);
        Assert.Equal("clinic", repository.CheckedCode);
        Assert.Equal(parentId, repository.CheckedParentId);

        var added = Assert.IsType<AccessScope>(repository.Added);
        var data = Assert.IsType<ScopeCreatedDetails>(result.Data);

        Assert.Equal(added.Id, data.ScopeId);
        Assert.Equal("clinic", data.ScopeCode);
        Assert.Equal("Clinic", data.DisplayName);
        Assert.Equal(parentId, data.ParentScopeId);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Duplicate_code_returns_before_parent_lookup()
    {
        var repository = new FakeRepository { CodeExists = true };

        var result = await new CreateScopeCommandHandler(repository)
            .Handle(
                new CreateScopeCommand(
                    "clinic", "Clinic", Guid.NewGuid()),
                CancellationToken.None);

        Assert.Equal(ScopeCreationStatus.CodeConflict, result.Status);
        Assert.Null(result.Data);
        Assert.Null(repository.CheckedParentId);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Create_maps_duplicate_detected_during_save()
    {
        var repository = new FakeRepository
        {
            SaveFailure = new DuplicateCodeException(
                CodeOwner.Scope,
                "clinic")
        };

        var result = await new CreateScopeCommandHandler(repository).Handle(
            new CreateScopeCommand(" clinic ", "Clinic", null),
            CancellationToken.None);

        Assert.Equal(ScopeCreationStatus.CodeConflict, result.Status);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Missing_parent_does_not_add_or_save()
    {
        var repository = new FakeRepository { ParentExists = false };

        var result = await new CreateScopeCommandHandler(repository)
            .Handle(
                new CreateScopeCommand(
                    "clinic", "Clinic", Guid.NewGuid()),
                CancellationToken.None);

        Assert.Equal(ScopeCreationStatus.ParentNotFound, result.Status);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Invalid_rename_is_rejected_before_lookup()
    {
        var repository = new FakeRepository();

        await Assert.ThrowsAsync<ScopeInputException>(async () =>
        {
            await new RenameScopeCommandHandler(repository).Handle(
                new RenameScopeCommand(Guid.NewGuid(), " "),
                CancellationToken.None);
        });

        Assert.Equal(0, repository.FindCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Rename_changes_loaded_entity_and_saves_once()
    {
        var scope = new AccessScope("clinic", "Old");
        var repository = new FakeRepository { Existing = scope };

        var result = await new RenameScopeCommandHandler(repository)
            .Handle(
                new RenameScopeCommand(scope.Id, " New "),
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(scope.Id, result!.ScopeId);
        Assert.Equal("New", result.DisplayName);
        Assert.Equal("New", scope.DisplayName);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Missing_scope_returns_null_without_saving()
    {
        var repository = new FakeRepository();

        var result = await new RenameScopeCommandHandler(repository)
            .Handle(
                new RenameScopeCommand(Guid.NewGuid(), "New"),
                CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, repository.SaveCalls);
    }

    private sealed class FakeRepository : IScopeRepository
    {
        public bool CodeExists { get; init; }
        public Exception? SaveFailure { get; init; }
        public bool ParentExists { get; init; } = true;
        public AccessScope? Existing { get; init; }
        public AccessScope? Added { get; private set; }
        public string? CheckedCode { get; private set; }
        public Guid? CheckedParentId { get; private set; }
        public int FindCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public Task<bool> ExistsByCodeAsync(
            string code, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckedCode = code;
            return Task.FromResult(CodeExists);
        }

        public Task<bool> ExistsByIdAsync(
            Guid scopeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckedParentId = scopeId;
            return Task.FromResult(ParentExists);
        }

        public Task<AccessScope?> FindForUpdateAsync(
            Guid scopeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FindCalls++;
            return Task.FromResult(
                Existing?.Id == scopeId ? Existing : null);
        }

        public void Add(AccessScope scope) => Added = scope;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCalls++;

            return SaveFailure is null
                ? Task.CompletedTask
                : Task.FromException(SaveFailure);
        }
    }
}