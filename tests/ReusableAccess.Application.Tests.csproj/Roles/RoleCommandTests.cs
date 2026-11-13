using ReusableAccess.Application.Roles;
using ReusableAccess.Domain.Entities;
using Xunit;

// تمام assertionهای این فایل باید از همان فریم‌ورک اجرای تست باشند.
using Assert = Xunit.Assert;

public sealed class RoleCommandTests
{
    [Fact]
    public async Task Create_checks_and_saves_normalized_code()
    {
        var repository = new FakeRoleRepository();
        var handler = new CreateRoleCommandHandler(repository);

        var result = await handler.Handle(
            new CreateRoleCommand(" admin ", " Administrator "),
            CancellationToken.None);

        Assert.False(result.AlreadyExists);
        Assert.Equal("admin", repository.CheckedCode);

        var addedRole = Assert.IsType<Role>(repository.AddedRole);

        Assert.Equal("admin", addedRole.Code);
        Assert.Equal("Administrator", addedRole.DisplayName);
        Assert.Equal(addedRole.Id, result.RoleId);
        Assert.Equal(addedRole.Code, result.RoleCode);
        Assert.Equal(addedRole.DisplayName, result.DisplayName);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Create_does_not_add_or_save_duplicate_role()
    {
        var repository = new FakeRoleRepository
        {
            CodeExists = true
        };

        var handler = new CreateRoleCommandHandler(repository);

        var result = await handler.Handle(
            new CreateRoleCommand("admin", "Administrator"),
            CancellationToken.None);

        Assert.True(result.AlreadyExists);
        Assert.Null(result.RoleId);
        Assert.Null(result.RoleCode);
        Assert.Null(result.DisplayName);
        Assert.Null(repository.AddedRole);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Rename_changes_loaded_entity_and_saves_once()
    {
        var role = new Role("admin", "Old name");

        var repository = new FakeRoleRepository
        {
            ExistingRole = role
        };

        var handler = new RenameRoleCommandHandler(repository);

        var result = await handler.Handle(
            new RenameRoleCommand(role.Id, " New name "),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(role.Id, repository.RequestedRoleId);
        Assert.Equal(role.Id, result!.RoleId);
        Assert.Equal("admin", result.RoleCode);
        Assert.Equal("New name", result.DisplayName);
        Assert.Equal("New name", role.DisplayName);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Rename_returns_null_without_saving_when_role_is_missing()
    {
        var repository = new FakeRoleRepository();
        var handler = new RenameRoleCommandHandler(repository);

        var result = await handler.Handle(
            new RenameRoleCommand(Guid.NewGuid(), "New name"),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Rename_does_not_save_when_domain_rejects_name()
    {
        var role = new Role("admin", "Old name");

        var repository = new FakeRoleRepository
        {
            ExistingRole = role
        };

        var handler = new RenameRoleCommandHandler(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
            {
                await handler.Handle(
                    new RenameRoleCommand(
                        role.Id,
                        new string('x', 201)),
                    CancellationToken.None);
            });

        Assert.Equal("Old name", role.DisplayName);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Create_forwards_cancellation_token_to_repository()
    {
        using var source = new CancellationTokenSource();

        var repository = new FakeRoleRepository();
        var handler = new CreateRoleCommandHandler(repository);

        await handler.Handle(
            new CreateRoleCommand("admin", "Administrator"),
            source.Token);

        Assert.Equal(source.Token, repository.ExistsToken);
        Assert.Equal(source.Token, repository.SaveToken);
    }

    [Fact]
    public async Task Create_stops_before_repository_when_already_cancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        var repository = new FakeRoleRepository();
        var handler = new CreateRoleCommandHandler(repository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                await handler.Handle(
                    new CreateRoleCommand("admin", "Administrator"),
                    source.Token);
            });

        Assert.Null(repository.CheckedCode);
        Assert.Null(repository.AddedRole);
        Assert.Equal(0, repository.SaveCalls);
    }

    // Fake فقط رفتار موردنیاز تست Application را مدل می‌کند.
    // این کلاس جایگزین تست یکپارچگی EF و SQL Server نیست.
    private sealed class FakeRoleRepository : IRoleRepository
    {
        public bool CodeExists { get; init; }

        public Role? ExistingRole { get; init; }

        public Role? AddedRole { get; private set; }

        public string? CheckedCode { get; private set; }

        public Guid? RequestedRoleId { get; private set; }

        public int SaveCalls { get; private set; }

        public CancellationToken ExistsToken { get; private set; }

        public CancellationToken SaveToken { get; private set; }

        public Task<bool> ExistsByCodeAsync(
            string code,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CheckedCode = code;
            ExistsToken = cancellationToken;

            return Task.FromResult(CodeExists);
        }

        public Task<Role?> FindForUpdateAsync(
            Guid roleId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RequestedRoleId = roleId;

            var role = ExistingRole?.Id == roleId
                ? ExistingRole
                : null;

            return Task.FromResult(role);
        }

        public void Add(Role role)
        {
            AddedRole = role;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveToken = cancellationToken;
            SaveCalls++;

            return Task.CompletedTask;
        }
    }
}