using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Completion;
using ReusableAccess.Infrastructure.Persistence;
using ReusableAccess.Infrastructure.Roles;
using ReusableAccess.Infrastructure.Scopes;
using Xunit;
using Assert = Xunit.Assert;

namespace ReusableAccess.Infrastructure.Tests;

public sealed class AccessPersistenceSqlServerTests
{
    [AccessPersistenceSqlServerFact]
    public async Task Role_duplicate_after_precheck_is_translated_and_cleaned()
    {
        await WithDatabaseAsync(async options =>
        {
            await using var loser = new ApplicationDbContext(options);
            var repository = new RoleRepository(loser);

            Assert.False(await repository.ExistsByCodeAsync(
                "admin",
                CancellationToken.None));

            // Context دیگر بعد از بررسی درخواست اول، برندهٔ درج می‌شود.
            await using (var winner = new ApplicationDbContext(options))
            {
                winner.Roles.Add(new Role("admin", "Winner"));
                await winner.SaveChangesAsync();
            }

            var candidate = new Role("admin", "Loser");
            repository.Add(candidate);

            var error = await Assert.ThrowsAsync<DuplicateCodeException>(
                () => repository.SaveChangesAsync(CancellationToken.None));

            Assert.Equal(CodeOwner.Role, error.Owner);
            Assert.Equal("admin", error.Code);

            // شامل پاک‌سازی Audit ساخته‌شده در Save ناموفق نیز هست.
            Assert.False(loser.ChangeTracker.HasChanges());

            await using var verification = new ApplicationDbContext(options);

            Assert.Equal(
                1,
                await verification.Roles.CountAsync(
                    role => role.Code == "admin"));
        });
    }

    [AccessPersistenceSqlServerFact]
    public async Task Scope_duplicate_after_precheck_is_translated_and_cleaned()
    {
        await WithDatabaseAsync(async options =>
        {
            await using var loser = new ApplicationDbContext(options);
            var repository = new ScopeRepository(loser);

            Assert.False(await repository.ExistsByCodeAsync(
                "clinic",
                CancellationToken.None));

            await using (var winner = new ApplicationDbContext(options))
            {
                winner.Set<AccessScope>().Add(
                    new AccessScope("clinic", "Winner"));

                await winner.SaveChangesAsync();
            }

            repository.Add(new AccessScope("clinic", "Loser"));

            var error = await Assert.ThrowsAsync<DuplicateCodeException>(
                () => repository.SaveChangesAsync(CancellationToken.None));

            Assert.Equal(CodeOwner.Scope, error.Owner);
            Assert.Equal("clinic", error.Code);
            Assert.False(loser.ChangeTracker.HasChanges());
        });
    }

    [AccessPersistenceSqlServerFact]
    public async Task Foreign_key_failure_is_not_reported_as_duplicate_code()
    {
        await WithDatabaseAsync(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var repository = new ScopeRepository(db);

            repository.Add(
                new AccessScope(
                    "clinic",
                    "Clinic",
                    parentScopeId: Guid.NewGuid()));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => repository.SaveChangesAsync(CancellationToken.None));
        });
    }

    [AccessPersistenceSqlServerFact]
    public async Task Scope_with_child_is_not_deleted()
    {
        await WithDatabaseAsync(async options =>
        {
            Guid parentId;

            await using (var seed = new ApplicationDbContext(options))
            {
                var parent = new AccessScope("parent", "Parent");
                var child = new AccessScope("child", "Child", parent.Id);

                seed.AddRange(parent, child);
                await seed.SaveChangesAsync();

                parentId = parent.Id;
            }

            await using var db = new ApplicationDbContext(options);

            var handler = new DeleteScopeCommandHandler(
                new ScopeDeletionRepository(db),
                new EfWriteTransactionFactory(db));

            var result = await handler.Handle(
                new DeleteScopeCommand(parentId),
                CancellationToken.None);

            Assert.Equal(ScopeDeletionStatus.HasDependencies, result);
            Assert.Null(db.Database.CurrentTransaction);

            Assert.True(await db.Set<AccessScope>().AnyAsync(
                scope => scope.Id == parentId));
        });
    }

    [AccessPersistenceSqlServerFact]
    public async Task Scope_without_dependencies_is_deleted_and_committed()
    {
        await WithDatabaseAsync(async options =>
        {
            Guid scopeId;

            await using (var seed = new ApplicationDbContext(options))
            {
                var scope = new AccessScope("clinic", "Clinic");

                seed.Add(scope);
                await seed.SaveChangesAsync();

                scopeId = scope.Id;
            }

            await using (var db = new ApplicationDbContext(options))
            {
                var handler = new DeleteScopeCommandHandler(
                    new ScopeDeletionRepository(db),
                    new EfWriteTransactionFactory(db));

                var result = await handler.Handle(
                    new DeleteScopeCommand(scopeId),
                    CancellationToken.None);

                Assert.Equal(ScopeDeletionStatus.Deleted, result);
                Assert.Null(db.Database.CurrentTransaction);
            }

            await using var verification = new ApplicationDbContext(options);

            Assert.False(await verification.Set<AccessScope>().AnyAsync(
                scope => scope.Id == scopeId));
        });
    }

    private static async Task WithDatabaseAsync(
        Func<DbContextOptions<ApplicationDbContext>, Task> test)
    {
        var template = Environment.GetEnvironmentVariable(
            "REUSABLEACCESS_TEST_SQLSERVER");

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException(
                "REUSABLEACCESS_TEST_SQLSERVER is not configured.");
        }

        // نام دیتابیس برنامه هرگز استفاده نمی‌شود.
        // هر تست دیتابیس مستقل خود را ایجاد و حذف می‌کند.
        var connectionString = new SqlConnectionStringBuilder(template)
        {
            InitialCatalog = $"ReusableAccess_Test_{Guid.NewGuid():N}",
            Pooling = false
        }.ConnectionString;

        var optionsBuilder =
     new DbContextOptionsBuilder<ApplicationDbContext>();

        optionsBuilder.UseSqlServer(connectionString);

        optionsBuilder.UseOpenIddict<Guid>();

        optionsBuilder.AddInterceptors(
            new SecurityAuditInterceptor(
                new HttpContextAccessor(),
                TimeProvider.System));

        DbContextOptions<ApplicationDbContext> options =
            optionsBuilder.Options;

        await using var schema = new ApplicationDbContext(options);

        try
        {
            await schema.Database.EnsureCreatedAsync();
            await test(options);
        }
        finally
        {
            await schema.Database.EnsureDeletedAsync();
        }
    }
}

public sealed class AccessPersistenceSqlServerFactAttribute : FactAttribute
{
    public AccessPersistenceSqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    "REUSABLEACCESS_TEST_SQLSERVER")))
        {
            Skip =
                "Set REUSABLEACCESS_TEST_SQLSERVER to an isolated test server.";
        }
    }
}