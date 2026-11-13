using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Domain.Entities;
using Xunit;
using Assert = Xunit.Assert;

namespace ReusableAccess.Application.Tests.RolePermissions;

public sealed class RolePermissionsCommandTests
{
    [Fact]
    public async Task Invalid_keys_are_rejected_before_role_lookup()
    {
        var repository = new FakeRepository();

        var result = await new ReplaceRolePermissionsCommandHandler(repository)
            .Handle(
                new ReplaceRolePermissionsCommand(Guid.NewGuid(), null),
                CancellationToken.None);

        Assert.Equal(
            RolePermissionsReplacementStatus.InvalidRequest,
            result.Status);

        Assert.Equal(0, repository.FindCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Missing_role_does_not_query_permissions_or_save()
    {
        var repository = new FakeRepository();

        var result = await new ReplaceRolePermissionsCommandHandler(repository)
            .Handle(
                new ReplaceRolePermissionsCommand(
                    Guid.NewGuid(), ["users.read"]),
                CancellationToken.None);

        Assert.Equal(
            RolePermissionsReplacementStatus.RoleNotFound,
            result.Status);

        Assert.Null(repository.CheckedKeys);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Unknown_keys_do_not_mutate_existing_permissions()
    {
        var role = new Role("admin", "Administrator");
        var existing = new Permission("users.read", "Read users");
        role.AddPermission(existing);

        var repository = new FakeRepository { ExistingRole = role };

        var result = await new ReplaceRolePermissionsCommandHandler(repository)
            .Handle(
                new ReplaceRolePermissionsCommand(
                    role.Id, ["missing.permission"]),
                CancellationToken.None);

        Assert.Equal(
            RolePermissionsReplacementStatus.UnknownPermissions,
            result.Status);

        Assert.Equal(
            new[] { "missing.permission" },
            result.UnknownPermissionKeys);

        Assert.Equal(
            existing.Id,
            Assert.Single(role.Permissions).PermissionId);

        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Replace_normalizes_deduplicates_and_saves_once()
    {
        var role = new Role("admin", "Administrator");
        var removed = new Permission("old.permission", "Old");
        var read = new Permission("users.read", "Read users");
        var write = new Permission("roles.write", "Write roles");

        role.AddPermission(removed);

        var repository = new FakeRepository
        {
            ExistingRole = role,
            AvailablePermissions = [read, write]
        };

        var result = await new ReplaceRolePermissionsCommandHandler(repository)
            .Handle(
                new ReplaceRolePermissionsCommand(
                    role.Id,
                    [" users.read ", "roles.write", "users.read"]),
                CancellationToken.None);

        Assert.Equal(
            RolePermissionsReplacementStatus.Updated,
            result.Status);

        Assert.Equal(
            new[] { "users.read", "roles.write" },
            repository.CheckedKeys);

        Assert.Equal(
            new[] { "roles.write", "users.read" },
            result.PermissionKeys);

        Assert.Empty(result.UnknownPermissionKeys);
        Assert.Equal(2, role.Permissions.Count);

        Assert.DoesNotContain(
            role.Permissions,
            item => item.PermissionId == removed.Id);

        Assert.Contains(
            role.Permissions,
            item => item.PermissionId == read.Id);

        Assert.Contains(
            role.Permissions,
            item => item.PermissionId == write.Id);

        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Empty_array_removes_all_permissions()
    {
        var role = new Role("admin", "Administrator");
        role.AddPermission(new Permission("users.read", "Read users"));

        var repository = new FakeRepository { ExistingRole = role };

        var result = await new ReplaceRolePermissionsCommandHandler(repository)
            .Handle(
                new ReplaceRolePermissionsCommand(role.Id, []),
                CancellationToken.None);

        Assert.Equal(
            RolePermissionsReplacementStatus.Updated,
            result.Status);

        Assert.Empty(role.Permissions);
        Assert.Empty(result.PermissionKeys);
        Assert.Equal(1, repository.SaveCalls);
    }

    private sealed class FakeRepository : IRolePermissionsRepository
    {
        public Role? ExistingRole { get; init; }

        public Permission[] AvailablePermissions { get; init; } = [];

        public string[]? CheckedKeys { get; private set; }

        public int FindCalls { get; private set; }

        public int SaveCalls { get; private set; }

        public Task<Role?> FindRoleForUpdateAsync(
            Guid roleId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FindCalls++;

            return Task.FromResult(
                ExistingRole?.Id == roleId ? ExistingRole : null);
        }

        public Task<IReadOnlyList<Permission>> FindPermissionsByKeysAsync(
            string[] keys,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckedKeys = keys.ToArray();

            IReadOnlyList<Permission> matches = AvailablePermissions
                .Where(permission => keys.Contains(
                    permission.Key,
                    StringComparer.Ordinal))
                .ToArray();

            return Task.FromResult(matches);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCalls++;
            return Task.CompletedTask;
        }
    }
}