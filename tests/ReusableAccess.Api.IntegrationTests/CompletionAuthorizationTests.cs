using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using ReusableAccess.Api;
using ReusableAccess.Api.Authorization;
using ReusableAccess.Api.Completion;
using ReusableAccess.Api.Controllers;
using ReusableAccess.Application;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Completion;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;
using ReusableAccess.Infrastructure.Scopes;
using Xunit;

namespace ReusableAccess.Api.IntegrationTests;

public sealed partial class CompletionAuthorizationTests
{
    private const string ConsoleWorkItems = "/api/console/work-items";
    private const string WorkItems = "/api/work-items";
    private const string ManagedUsers = "/api/admin/users/manage";

    [Fact]
    public async Task Password_post_without_csrf_is_rejected()
    {
        await using var host = await Host.Create();

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["nationalId"] = "0012345679",
                ["password"] = "Test-Only.Strong42!"
            });

        using var response = await host.Client.PostAsync(
            "/account/form/password", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_admin_request_is_401()
    {
        await using var host = await Host.Create();

        using var response = await host.Client.GetAsync(ManagedUsers);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Regular_user_cannot_manage_accounts()
    {
        await using var host = await Host.Create();
        host.As(host.Reader);

        using var response = await host.Client.GetAsync(ManagedUsers);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Real_admin_policy_allows_listing_users()
    {
        await using var host = await Host.Create();
        host.As(host.Admin);

        using var response = await host.Client.GetAsync(ManagedUsers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Real_admin_policy_allows_listing_scopes()
    {
        await using var host = await Host.Create();
        host.As(host.Admin);

        using var response = await host.Client.GetAsync("/api/admin/scopes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Header_cannot_grant_all_scopes()
    {
        await using var host = await Host.Create();
        host.As(host.Reader, mode: "all");

        using var response = await host.Client.GetAsync(ConsoleWorkItems);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cross_scope_id_is_404_even_with_update_permission()
    {
        await using var host = await Host.Create();
        host.As(host.Reader);

        using var allowed = await host.Client.PutAsJsonAsync(
            ItemUrl(host.OwnItem),
            new WorkItemInput(Guid.Empty, "Updated in allowed scope", "Closed"));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var outsideScope = await host.Client.PutAsJsonAsync(
            ItemUrl(host.OtherItem),
            new WorkItemInput(Guid.Empty, "Must not be updated", "Closed"));

        Assert.Equal(HttpStatusCode.NotFound, outsideScope.StatusCode);
    }

    [Fact]
    public async Task Permission_removal_works_without_new_login()
    {
        await using var host = await Host.Create();
        host.As(host.Reader);

        using var before = await host.Client.GetAsync(ConsoleWorkItems);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var permissions = await db.Set<RolePermission>().ToListAsync();
            db.RemoveRange(permissions);
            await db.SaveChangesAsync();
        }

        using var after = await host.Client.GetAsync(ConsoleWorkItems);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    }

    [Fact]
    public async Task Disabled_account_is_rejected_before_permission_check()
    {
        await using var host = await Host.Create();
        host.As(host.Reader);

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == host.Reader);

            user.IsActive = false;
            user.SessionVersion++;

            await db.SaveChangesAsync();
        }

        using var response = await host.Client.GetAsync(ConsoleWorkItems);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Work_item_create_and_delete_preserve_contract(bool allScopes)
    {
        await using var host = await Host.Create(
            allowCreateAndDelete: true,
            useAllScopes: allScopes);

        host.As(host.Reader, mode: allScopes ? "all" : "selected");

        // در selected، مقصد از محدودهٔ فعال می‌آید.
        // در all، مقصد از بدنهٔ درخواست می‌آید.
        var requestedScopeId = allScopes ? host.ScopeBId : Guid.Empty;
        var expectedScopeId = allScopes ? host.ScopeBId : host.ScopeAId;

        using var created = await host.Client.PostAsJsonAsync(
            WorkItems,
            new WorkItemInput(requestedScopeId, "Contract test item", "Open"));

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        using var createBody = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());

        var itemId = createBody.RootElement.GetProperty("data").GetGuid();
        Assert.NotEqual(Guid.Empty, itemId);

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.Set<WorkItem>().SingleAsync(x => x.Id == itemId);

            Assert.Equal(expectedScopeId, item.ScopeId);
        }

        using var deleted = await host.Client.DeleteAsync(ItemUrl(itemId));

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        using var deleteBody = JsonDocument.Parse(
            await deleted.Content.ReadAsStringAsync());

        Assert.True(deleteBody.RootElement.GetProperty("data").GetBoolean());

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.False(await db.Set<WorkItem>().AnyAsync(x => x.Id == itemId));
        }
    }

    [Fact]
    public async Task Selected_scope_rejects_writes_to_another_scope()
    {
        await using var host = await Host.Create(allowCreateAndDelete: true);
        host.As(host.Reader);

        const string title = "Out of scope item";

        using var create = await host.Client.PostAsJsonAsync(
            WorkItems,
            new WorkItemInput(host.ScopeBId, title, "Open"));

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var delete = await host.Client.DeleteAsync(ItemUrl(host.OtherItem));
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await db.Set<WorkItem>().AnyAsync(x => x.Title == title));
        Assert.True(await db.Set<WorkItem>().AnyAsync(x => x.Id == host.OtherItem));
    }

    [Fact]
    public async Task Work_item_create_and_delete_require_their_own_permissions()
    {
        await using var host = await Host.Create();
        host.As(host.Reader);

        // این کاربر read و update دارد، اما create و delete ندارد.
        const string title = "Missing permission item";

        using var create = await host.Client.PostAsJsonAsync(
            WorkItems,
            new WorkItemInput(Guid.Empty, title, "Open"));

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var delete = await host.Client.DeleteAsync(ItemUrl(host.OwnItem));
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await db.Set<WorkItem>().AnyAsync(x => x.Title == title));
        Assert.True(await db.Set<WorkItem>().AnyAsync(x => x.Id == host.OwnItem));
    }

    private static string ItemUrl(Guid id) => $"{WorkItems}/{id}";

    // فقط در TestServer استفاده می‌شود؛ Policyهای مجوز API همچنان اجرا می‌شوند.
    private sealed class TestAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userHeader = Request.Headers["X-Test-User"].FirstOrDefault();

            if (!Guid.TryParse(userHeader, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new Claim[]
            {
                new("sub", userId.ToString()),
                new(OpenIddictConstants.Claims.Scope, "access_api"),
                new("ra_version", "0")
            };

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, Scheme.Name));

            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class Host : IAsyncDisposable
    {
        private const string EditorRole = "TEST_EDITOR";
        private const string ScopeACode = "A";

        private readonly SqliteConnection _connection;

        private Host(WebApplication app, SqliteConnection connection)
        {
            App = app;
            _connection = connection;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public Guid Admin { get; } = Guid.NewGuid();
        public Guid Reader { get; } = Guid.NewGuid();

        public Guid ScopeAId { get; private set; }
        public Guid ScopeBId { get; private set; }
        public Guid OwnItem { get; private set; }
        public Guid OtherItem { get; private set; }

        public static async Task<Host> Create(
            bool allowCreateAndDelete = false,
            bool useAllScopes = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            WebApplication? app = null;

            try
            {
                await connection.OpenAsync();

                var builder = WebApplication.CreateBuilder(
                    new WebApplicationOptions { EnvironmentName = "Testing" });

                builder.WebHost.UseTestServer();

                var services = builder.Services;
                services.AddDbContext<ApplicationDbContext>(
                    options => options.UseSqlite(connection));

                var apiScheme =
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;

                services.AddAuthentication(apiScheme)
                    .AddCookie(IdentityConstants.ApplicationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuth>(
                        apiScheme, _ => { });

                services.AddIdentityCore<ApplicationUser>()
                    .AddSignInManager()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddDefaultTokenProviders();

                services.AddApplication();
                services.AddControllers()
                    .AddApplicationPart(typeof(AdminScopesController).Assembly);

                services.AddScoped<IScopeQueries, ScopeQueries>();
                services.AddScoped<IScopeRepository, ScopeRepository>();

                services.AddAccessFeaturesInfrastructure();
                services.AddProblemDetails();
                services.AddReusableAccessAuthorization();
                services.AddReusableAccessApiAuthorization();
                services.AddCompletionApi();

                var cleanup = services.FirstOrDefault(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType == typeof(ChallengeCleanup));

                if (cleanup is not null)
                {
                    services.Remove(cleanup);
                }

                app = builder.Build();

                app.UseExceptionHandler();
                app.UseAuthentication();
                app.UseCompletionAccountGate();
                app.UseAuthorization();
                app.UseRateLimiter();
                app.UseAntiforgery();

                app.MapCompletionEndpoints();
                app.MapAccountForms();
                app.MapControllers();

                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();

                    await db.Database.EnsureCreatedAsync();
                }

                await app.StartAsync();

                var host = new Host(app, connection);

                try
                {
                    await host.SeedAsync(allowCreateAndDelete, useAllScopes);
                    return host;
                }
                catch
                {
                    host.Client.Dispose();
                    throw;
                }
            }
            catch
            {
                try
                {
                    if (app is not null)
                    {
                        await app.DisposeAsync();
                    }
                }
                finally
                {
                    await connection.DisposeAsync();
                }

                throw;
            }
        }

        private async Task SeedAsync(
            bool allowCreateAndDelete,
            bool useAllScopes)
        {
            await using var scope = App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Users.AddRange(
                new ApplicationUser
                {
                    Id = Admin,
                    UserName = "admin",
                    NationalId = "admin-test",
                    DisplayName = "Admin",
                    IsPlatformSuperAdmin = true,
                    IsActive = true
                },
                new ApplicationUser
                {
                    Id = Reader,
                    UserName = "reader",
                    NationalId = "reader-test",
                    DisplayName = "Reader",
                    IsActive = true
                });

            var scopeA = new AccessScope(ScopeACode, "A");
            var scopeB = new AccessScope("B", "B");

            ScopeAId = scopeA.Id;
            ScopeBId = scopeB.Id;

            var role = new Role(EditorRole, "Editor");
            var read = new Permission("workitems.read", "read");
            var update = new Permission("workitems.update", "update");

            role.AddPermission(read);
            role.AddPermission(update);

            if (allowCreateAndDelete)
            {
                var create = new Permission("workitems.create", "create work item");
                var delete = new Permission("workitems.delete", "delete work item");

                role.AddPermission(create);
                role.AddPermission(delete);

                db.AddRange(create, delete);
            }

            db.AddRange(scopeA, scopeB, role, read, update);
            await db.SaveChangesAsync();

            // هدر all کافی نیست؛ کاربر باید Bundle از نوع All داشته باشد.
            var mode = useAllScopes
                ? ScopeSelectionMode.All
                : ScopeSelectionMode.Selected;

            var bundleScopeIds = useAllScopes
                ? Array.Empty<Guid>()
                : new[] { ScopeAId };

            var bundle = new UserAccessBundle(
                Reader,
                useAllScopes ? "All scopes" : "A only",
                [role.Id],
                mode,
                bundleScopeIds);

            var own = new WorkItem(ScopeAId, "Own scope item", "Open");
            var other = new WorkItem(ScopeBId, "Other scope item", "Open");

            OwnItem = own.Id;
            OtherItem = other.Id;

            db.AddRange(bundle, own, other);
            await db.SaveChangesAsync();

            Assert.True(await db.Set<RolePermission>().AnyAsync(x =>
                x.RoleId == role.Id && x.PermissionId == update.Id));

            Assert.True(await db.Set<UserAccessBundleRole>().AnyAsync(x =>
                x.RoleId == role.Id));

            if (!useAllScopes)
            {
                Assert.True(await db.Set<UserAccessBundleScope>().AnyAsync(x =>
                    x.ScopeId == ScopeAId));
            }
        }

        public void As(Guid userId, string mode = "selected")
        {
            SetHeader("X-Test-User", userId.ToString());
            SetHeader("X-Active-Role", EditorRole);
            SetHeader("X-Active-Scope", ScopeACode);
            SetHeader("X-Active-Scope-Mode", mode);
        }

        private void SetHeader(string name, string value)
        {
            Client.DefaultRequestHeaders.Remove(name);
            Client.DefaultRequestHeaders.Add(name, value);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();

            try
            {
                await App.DisposeAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}