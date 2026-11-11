using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application;
using ReusableAccess.Application.Completion;
using ReusableAccess.Application.Users;
using ReusableAccess.Contracts;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Completion;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class SqlServerUserFactAttribute : FactAttribute
{
    public SqlServerUserFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("RA_SQLSERVER_TEST_CONNECTION")))
        {
            Skip = "RA_SQLSERVER_TEST_CONNECTION is not configured.";
        }
    }
}

[Trait("Category", "SqlServer")]
public sealed class SqlServerUserConcurrencyTests
{
    [SqlServerUserFact]
    public async Task Concurrent_create_keeps_one_national_id()
    {
        await using var database = await TestDatabase.CreateAsync();

        var results = await database.Race(
            async sender =>
            {
                await sender.Send(new CreateManagedUser(
                    database.AdminA, NewUser()));
            },
            async sender =>
            {
                await sender.Send(new CreateManagedUser(
                    database.AdminA, NewUser()));
            });

        Assert.Equal(new[] { 200, 409 }, results.OrderBy(code => code).ToArray());

        var count = await database.InScope(async services =>
            await services.GetRequiredService<ApplicationDbContext>()
                .Users.CountAsync(user => user.NationalId == "0012345679"));

        Assert.Equal(1, count);
    }

    [SqlServerUserFact]
    public async Task Concurrent_edits_with_one_stamp_have_one_winner()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = await database.Read(database.AdminB);

        var results = await database.Race(
            async sender =>
            {
                await sender.Send(new UpdateManagedUser(
                    database.AdminA,
                    database.AdminB,
                    Update(original, "Winner-A")));
            },
            async sender =>
            {
                await sender.Send(new UpdateManagedUser(
                    database.AdminA,
                    database.AdminB,
                    Update(original, "Winner-B")));
            });

        Assert.Equal(new[] { 200, 409 }, results.OrderBy(code => code).ToArray());

        var current = await database.Read(database.AdminB);
        Assert.Contains(current.FirstName, new[] { "Winner-A", "Winner-B" });
        Assert.NotEqual(original.ConcurrencyStamp, current.ConcurrencyStamp);
    }

    [SqlServerUserFact]
    public async Task Concurrent_admin_deactivation_keeps_an_active_admin()
    {
        await using var database = await TestDatabase.CreateAsync();
        var a = await database.Read(database.AdminA);
        var b = await database.Read(database.AdminB);

        var results = await database.Race(
            async sender =>
            {
                await sender.Send(new SetManagedUserActive(
                    a.UserId,
                    b.UserId,
                    new ActiveUserInput(false, b.ConcurrencyStamp)));
            },
            async sender =>
            {
                await sender.Send(new SetManagedUserActive(
                    b.UserId,
                    a.UserId,
                    new ActiveUserInput(false, a.ConcurrencyStamp)));
            });

        Assert.Equal(new[] { 200, 409 }, results.OrderBy(code => code).ToArray());

        var activeAdmins = await database.InScope(async services =>
            await services.GetRequiredService<ApplicationDbContext>()
                .Users.CountAsync(user =>
                    user.IsActive && user.IsPlatformSuperAdmin));

        Assert.Equal(1, activeAdmins);
    }

    private static CreateUserInput NewUser() => new()
    {
        FirstName = "SQL",
        LastName = "Race",
        OrganizationalCode = "TEST-SQL",
        NationalId = "0012345679",
        Email = "sql-race@example.test",
        Mobile = "09123456789",
        Password = "Test-Only.Strong42!"
    };

    private static UpdateUserInput Update(UserRow row, string firstName) => new()
    {
        FirstName = firstName,
        LastName = row.LastName,
        OrganizationalCode = row.OrganizationalCode,
        NationalId = row.NationalId,
        Email = row.Email,
        Mobile = row.Mobile,
        ConcurrencyStamp = row.ConcurrencyStamp
    };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        public Guid AdminA { get; } = Guid.NewGuid();
        public Guid AdminB { get; } = Guid.NewGuid();

        private TestDatabase(ServiceProvider services)
            => _services = services;

        public static async Task<TestDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable(
                "RA_SQLSERVER_TEST_CONNECTION")
                ?? throw new InvalidOperationException("SQL test connection is missing.");

            // هر اجرا دیتابیس تازه دارد؛ نام دیتابیس ورودی استفاده نمی‌شود.
            var connection = new SqlConnectionStringBuilder(configured)
            {
                InitialCatalog = "RA_UserTests_" + Guid.NewGuid().ToString("N")
            };

            var services = new ServiceCollection();
            services.AddLogging();

            services.AddDbContext<ApplicationDbContext>((provider, options) =>
                options.UseSqlServer(connection.ConnectionString)
                    .AddInterceptors(
                        provider.GetRequiredService<SecurityAuditInterceptor>()));

            services.AddIdentityCore<ApplicationUser>()
                .AddSignInManager()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddApplication();
            services.AddCompletionInfrastructure();

            var database = new TestDatabase(services.BuildServiceProvider());

            try
            {
                await database.InScope(async provider =>
                {
                    var db = provider.GetRequiredService<ApplicationDbContext>();
                    await db.Database.EnsureCreatedAsync();

                    db.Users.AddRange(
                        Seed(database.AdminA, "1234567891", "AdminA"),
                        Seed(database.AdminB, "9876543210", "AdminB"));

                    await db.SaveChangesAsync();
                    return true;
                });

                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async Task<T> InScope<T>(
            Func<IServiceProvider, Task<T>> action)
        {
            await using var scope = _services.CreateAsyncScope();
            return await action(scope.ServiceProvider);
        }

        public Task<UserRow> Read(Guid id)
            => InScope(provider =>
                provider.GetRequiredService<IManagedUserReadStore>()
                    .CurrentUserAsync(id, CancellationToken.None));

        public async Task<int[]> Race(
            Func<ISender, Task> first,
            Func<ISender, Task> second)
        {
            var start = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Task<int> Attempt(Func<ISender, Task> command)
                => InScope(async provider =>
                {
                    await start.Task;

                    try
                    {
                        await command(provider.GetRequiredService<ISender>());
                        return 200;
                    }
                    catch (CompletionException error)
                    {
                        return error.StatusCode;
                    }
                });

            var left = Attempt(first);
            var right = Attempt(second);
            start.SetResult(true);

            return await Task.WhenAll(left, right);
        }

        private static ApplicationUser Seed(
            Guid id, string nationalId, string name) => new()
            {
                Id = id,
                UserName = nationalId,
                NormalizedUserName = nationalId,
                NationalId = nationalId,
                FirstName = name,
                LastName = "Test",
                DisplayName = $"{name} Test",
                OrganizationalCode = name,
                Email = $"{name}@example.test",
                NormalizedEmail = $"{name.ToUpperInvariant()}@EXAMPLE.TEST",
                PhoneNumber = "+989123456789",
                IsActive = true,
                IsPlatformSuperAdmin = true
            };

        public async ValueTask DisposeAsync()
        {
            try
            {
                await InScope(async provider =>
                {
                    var db = provider.GetRequiredService<ApplicationDbContext>();
                    var name = db.Database.GetDbConnection().Database;

                    if (!name.StartsWith(
                            "RA_UserTests_", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Refusing to delete a non-test database.");
                    }

                    await db.Database.EnsureDeletedAsync();
                    return true;
                });
            }
            finally
            {
                await _services.DisposeAsync();
            }
        }
    }
}