using ReusableAccess.Application.Bundles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using Xunit;
using Assert = Xunit.Assert;

namespace ReusableAccess.Application.Tests.Bundles;

public sealed class UserBundleCommandTests
{
    [Fact]
    public async Task Create_preserves_user_not_found_precedence()
    {
        var repository = new FakeRepository { UserExists = false };

        var result = await new CreateUserBundleCommandHandler(repository)
            .Handle(
                new CreateUserBundleCommand(
                    Guid.NewGuid(), null, null, null, null, -1, true),
                CancellationToken.None);

        Assert.Equal(BundleCreationStatus.UserNotFound, result.Status);
        Assert.Equal(0, repository.ReferenceChecks);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Create_normalizes_name_and_builds_disabled_bundle()
    {
        var repository = new FakeRepository();
        var roleId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await new CreateUserBundleCommandHandler(repository)
            .Handle(
                new CreateUserBundleCommand(
                    userId,
                    " Clinic ",
                    [roleId, roleId],
                    "SELECTED",
                    [scopeId, scopeId],
                    3,
                    false),
                CancellationToken.None);

        Assert.Equal(BundleCreationStatus.Created, result.Status);

        var bundle = Assert.IsType<UserAccessBundle>(repository.Added);

        Assert.Equal(bundle.Id, result.BundleId);
        Assert.Equal(userId, bundle.UserId);
        Assert.Equal("Clinic", bundle.Name);
        Assert.Equal(3, bundle.SortOrder);
        Assert.False(bundle.IsEnabled);
        Assert.Equal(ScopeSelectionMode.Selected, bundle.ScopeSelectionMode);
        Assert.Single(bundle.Roles);
        Assert.Single(bundle.Scopes);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Theory]
    [InlineData("all", true)]
    [InlineData("selected", false)]
    [InlineData("invalid", false)]
    public async Task Create_rejects_invalid_scope_definition(
        string mode,
        bool includeScope)
    {
        var repository = new FakeRepository();

        var result = await new CreateUserBundleCommandHandler(repository)
            .Handle(
                new CreateUserBundleCommand(
                    Guid.NewGuid(),
                    "Clinic",
                    [Guid.NewGuid()],
                    mode,
                    includeScope ? [Guid.NewGuid()] : [],
                    0,
                    true),
                CancellationToken.None);

        Assert.Equal(BundleCreationStatus.InvalidRequest, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(0, repository.ReferenceChecks);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Theory]
    [InlineData(true, false, "یک یا چند Role پیدا نشد.")]
    [InlineData(false, true, "یک یا چند Scope پیدا نشد.")]
    public async Task Create_rejects_missing_references(
        bool missingRoles,
        bool missingScopes,
        string expectedError)
    {
        var repository = new FakeRepository
        {
            MissingRoles = missingRoles,
            MissingScopes = missingScopes
        };

        var result = await new CreateUserBundleCommandHandler(repository)
            .Handle(
                new CreateUserBundleCommand(
                    Guid.NewGuid(),
                    "Clinic",
                    [Guid.NewGuid()],
                    "selected",
                    [Guid.NewGuid()],
                    0,
                    true),
                CancellationToken.None);

        Assert.Equal(BundleCreationStatus.InvalidRequest, result.Status);
        Assert.Equal(expectedError, result.ErrorMessage);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Invalid_update_does_not_mutate_loaded_bundle()
    {
        var roleId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var bundle = new UserAccessBundle(
            Guid.NewGuid(), "Old", [roleId],
            ScopeSelectionMode.Selected, [scopeId], 2);

        var repository = new FakeRepository { Existing = bundle };

        var result = await new UpdateUserBundleCommandHandler(repository)
            .Handle(
                new UpdateUserBundleCommand(
                    bundle.Id, "New", [Guid.NewGuid()],
                    "all", [scopeId], 9, false),
                CancellationToken.None);

        Assert.Equal(BundleUpdateStatus.InvalidRequest, result.Status);
        Assert.Equal("Old", bundle.Name);
        Assert.Equal(2, bundle.SortOrder);
        Assert.True(bundle.IsEnabled);
        Assert.Equal(roleId, Assert.Single(bundle.Roles).RoleId);
        Assert.Equal(scopeId, Assert.Single(bundle.Scopes).ScopeId);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Update_replaces_definition_and_saves_once()
    {
        var bundle = new UserAccessBundle(
            Guid.NewGuid(), "Old", [Guid.NewGuid()],
            ScopeSelectionMode.Selected, [Guid.NewGuid()]);

        var repository = new FakeRepository { Existing = bundle };
        var newRoleId = Guid.NewGuid();

        var result = await new UpdateUserBundleCommandHandler(repository)
            .Handle(
                new UpdateUserBundleCommand(
                    bundle.Id, " New ", [newRoleId],
                    "all", [], 4, false),
                CancellationToken.None);

        Assert.Equal(BundleUpdateStatus.Updated, result.Status);
        Assert.Equal("New", bundle.Name);
        Assert.Equal(4, bundle.SortOrder);
        Assert.False(bundle.IsEnabled);
        Assert.Equal(ScopeSelectionMode.All, bundle.ScopeSelectionMode);
        Assert.Empty(bundle.Scopes);
        Assert.Equal(newRoleId, Assert.Single(bundle.Roles).RoleId);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Missing_bundle_returns_before_definition_validation()
    {
        var repository = new FakeRepository();

        var result = await new UpdateUserBundleCommandHandler(repository)
            .Handle(
                new UpdateUserBundleCommand(
                    Guid.NewGuid(), null, null, null, null, -1, false),
                CancellationToken.None);

        Assert.Equal(BundleUpdateStatus.BundleNotFound, result.Status);
        Assert.Equal(0, repository.ReferenceChecks);
        Assert.Equal(0, repository.SaveCalls);
    }

    private sealed class FakeRepository : IUserBundleRepository
    {
        public bool UserExists { get; init; } = true;
        public bool MissingRoles { get; init; }
        public bool MissingScopes { get; init; }
        public UserAccessBundle? Existing { get; init; }
        public UserAccessBundle? Added { get; private set; }
        public int SaveCalls { get; private set; }
        public int ReferenceChecks { get; private set; }

        public Task<bool> UserExistsAsync(
            Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(UserExists);
        }

        public Task<int> CountExistingRolesAsync(
            Guid[] roleIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReferenceChecks++;
            return Task.FromResult(MissingRoles ? 0 : roleIds.Length);
        }

        public Task<int> CountExistingScopesAsync(
            Guid[] scopeIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReferenceChecks++;
            return Task.FromResult(MissingScopes ? 0 : scopeIds.Length);
        }

        public Task<UserAccessBundle?> FindForUpdateAsync(
            Guid bundleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                Existing?.Id == bundleId ? Existing : null);
        }

        public void Add(UserAccessBundle bundle) => Added = bundle;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCalls++;
            return Task.CompletedTask;
        }
    }
}