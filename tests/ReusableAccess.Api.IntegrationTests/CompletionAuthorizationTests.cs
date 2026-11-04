using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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
using ReusableAccess.Api.Completion;
using ReusableAccess.Application;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Authorization;
using ReusableAccess.Infrastructure.Completion;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class CompletionAuthorizationTests
{
    [Fact]
    public async Task Password_post_without_csrf_is_rejected()
    {
        await using var f = await Host.Create();

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["nationalId"] = "0012345679",
                ["password"] = "Test-Only.Strong42!"
            });

        var response = await f.Client.PostAsync("/account/form/password", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_admin_request_is_401()
    {
        await using var f = await Host.Create();
        var r = await f.Client.GetAsync("/api/admin/users/manage");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Regular_user_cannot_manage_accounts()
    {
        await using var f = await Host.Create();
        f.As(f.Reader);

        var r = await f.Client.GetAsync("/api/admin/users/manage");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Real_admin_policy_allows_listing()
    {
        await using var f = await Host.Create();
        f.As(f.Admin);

        var r = await f.Client.GetAsync("/api/admin/users/manage");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Header_cannot_grant_all_scopes()
    {
        await using var f = await Host.Create();
        f.As(f.Reader, "all");

        var r = await f.Client.GetAsync("/api/console/work-items");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Cross_scope_id_is_404_even_with_update_permission()
    {
        await using var f = await Host.Create();
        f.As(f.Reader);

        var r = await f.Client.PutAsJsonAsync(
            $"/api/work-items/{f.OtherItem}",
            new WorkItemInput(Guid.Empty, "not allowed", "Closed"));

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Permission_removal_works_without_new_login()
    {
        await using var f = await Host.Create();
        f.As(f.Reader);

        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.GetAsync("/api/console/work-items")).StatusCode);

        await using (var scope = f.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            db.RemoveRange(await db.Set<RolePermission>().ToListAsync());
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden,
            (await f.Client.GetAsync("/api/console/work-items")).StatusCode);
    }

    [Fact]
    public async Task Disabled_account_is_rejected_before_permission_check()
    {
        await using var f = await Host.Create();
        f.As(f.Reader);

        await using (var scope = f.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var user = await db.Users.SingleAsync(x => x.Id == f.Reader);
            user.IsActive = false;
            user.SessionVersion++;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await f.Client.GetAsync("/api/console/work-items")).StatusCode);
    }

    private sealed class TestAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(
            options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(
                    Request.Headers["X-Test-User"].FirstOrDefault(), out var id))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
            [
                new Claim("sub", id.ToString()),
                new Claim(OpenIddictConstants.Claims.Scope, "access_api"),
                new Claim("ra_version", "0")
            ], Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(
                    new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public Guid Admin { get; } = Guid.NewGuid();
        public Guid Reader { get; } = Guid.NewGuid();
        public Guid OtherItem { get; private set; }

        private Host(WebApplication app, SqliteConnection connection)
        {
            App = app;
            _connection = connection;
            Client = app.GetTestClient();
        }

        public static async Task<Host> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var b = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

            b.WebHost.UseTestServer();

            b.Services.AddDbContext<ApplicationDbContext>(
                o => o.UseSqlite(connection));

            b.Services.AddAuthentication(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuth>(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
                    _ => { });

            b.Services.AddIdentityCore<ApplicationUser>()
                .AddSignInManager()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            b.Services.AddApplication();
            b.Services.AddCompletionInfrastructure();
            b.Services.AddProblemDetails();
            b.Services.AddReusableAccessApiAuthorization();
            b.Services.AddCompletionApi();

            b.Services.AddScoped<
                IAuthorizationHandler, PermissionAuthorizationHandler>();

            var cleanup = b.Services.FirstOrDefault(x =>
                x.ServiceType == typeof(IHostedService) &&
                x.ImplementationType == typeof(ChallengeCleanup));

            if (cleanup is not null)
                b.Services.Remove(cleanup);

            var app = b.Build();

            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseCompletionAccountGate();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.UseAntiforgery();

            app.MapCompletionEndpoints();
            app.MapAccountForms();

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                await db.Database.EnsureCreatedAsync();
            }

            await app.StartAsync();
            var host = new Host(app, connection);

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                db.Users.AddRange(
                    new ApplicationUser
                    {
                        Id = host.Admin,
                        UserName = "admin",
                        NationalId = "admin-test",
                        DisplayName = "Admin",
                        IsPlatformSuperAdmin = true,
                        IsActive = true
                    },
                    new ApplicationUser
                    {
                        Id = host.Reader,
                        UserName = "reader",
                        NationalId = "reader-test",
                        DisplayName = "Reader",
                        IsActive = true
                    });

                var a = new AccessScope("A", "A");
                var bScope = new AccessScope("B", "B");
                var role = new Role("TEST_EDITOR", "Editor");
                var read = new Permission("workitems.read", "read");
                var update = new Permission("workitems.update", "update");

                db.AddRange(a, bScope, role, read, update);
                await db.SaveChangesAsync();

                role.AddPermission(read);
                role.AddPermission(update);

                db.Add(new UserAccessBundle(
                    host.Reader, "A only", [role.Id],
                    ScopeSelectionMode.Selected, [a.Id]));

                var item = new WorkItem(bScope.Id, "Other scope", "Open");
                host.OtherItem = item.Id;
                db.Add(item);

                await db.SaveChangesAsync();
            }

            return host;
        }

        public void As(Guid id, string mode = "selected")
        {
            Set("X-Test-User", id.ToString());
            Set("X-Active-Role", "TEST_EDITOR");
            Set("X-Active-Scope", "A");
            Set("X-Active-Scope-Mode", mode);
        }

        private void Set(string key, string value)
        {
            Client.DefaultRequestHeaders.Remove(key);
            Client.DefaultRequestHeaders.Add(key, value);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}