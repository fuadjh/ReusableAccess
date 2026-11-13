using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
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

        var response = await f.Client.PostAsync(
            "/account/form/password",
            form);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_admin_request_is_401()
    {
        await using var f = await Host.Create();

        var response = await f.Client.GetAsync(
            "/api/admin/users/manage");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Regular_user_cannot_manage_accounts()
    {
        await using var f = await Host.Create();
        f.As(f.Reader);

        var response = await f.Client.GetAsync(
            "/api/admin/users/manage");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Real_admin_policy_allows_listing_users()
    {
        await using var f = await Host.Create();
        f.As(f.Admin);

        var response = await f.Client.GetAsync(
            "/api/admin/users/manage");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Real_admin_policy_allows_listing_scopes()
    {
        await using var f = await Host.Create();
        f.As(f.Admin);

        var response = await f.Client.GetAsync(
            "/api/admin/scopes");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Header_cannot_grant_all_scopes()
    {
        await using var f = await Host.Create();
        f.As(f.Reader, "all");

        var response = await f.Client.GetAsync(
            "/api/console/work-items");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Cross_scope_id_is_404_even_with_update_permission()
    {
        await using var f = await Host.Create();
        f.As(f.Reader);

        // کنترل مثبت: کاربر در Scope فعال مجوز update دارد.
        using var sameScopeResponse = await f.Client.PutAsJsonAsync(
            $"/api/work-items/{f.OwnItem}",
            new WorkItemInput(
                Guid.Empty,
                "Updated in allowed scope",
                "Closed"));

        Assert.Equal(
            HttpStatusCode.OK,
            sameScopeResponse.StatusCode);

        // کنترل امنیتی: همان کاربر نباید مورد Scope دیگر را ببیند.
        using var crossScopeResponse = await f.Client.PutAsJsonAsync(
            $"/api/work-items/{f.OtherItem}",
            new WorkItemInput(
                Guid.Empty,
                "Must not be updated",
                "Closed"));

        Assert.Equal(
            HttpStatusCode.NotFound,
            crossScopeResponse.StatusCode);
    }

    [Fact]
    public async Task Permission_removal_works_without_new_login()
    {
        await using var f = await Host.Create();
        f.As(f.Reader);

        var beforeRemoval = await f.Client.GetAsync(
            "/api/console/work-items");

        Assert.Equal(
            HttpStatusCode.OK,
            beforeRemoval.StatusCode);

        await using (var scope = f.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            db.RemoveRange(
                await db.Set<RolePermission>().ToListAsync());

            await db.SaveChangesAsync();
        }

        var afterRemoval = await f.Client.GetAsync(
            "/api/console/work-items");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            afterRemoval.StatusCode);
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

            var user = await db.Users.SingleAsync(
                x => x.Id == f.Reader);

            user.IsActive = false;
            user.SessionVersion++;

            await db.SaveChangesAsync();
        }

        var response = await f.Client.GetAsync(
            "/api/console/work-items");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private sealed class TestAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(
            options,
            logger,
            encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(
                    Request.Headers["X-Test-User"].FirstOrDefault(),
                    out var id))
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
            [
                new Claim("sub", id.ToString()),
                new Claim(
                    OpenIddictConstants.Claims.Scope,
                    "access_api"),
                new Claim("ra_version", "0")
            ], Scheme.Name);

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(
                principal,
                Scheme.Name);

            return Task.FromResult(
                AuthenticateResult.Success(ticket));
        }
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public Guid Admin { get; } = Guid.NewGuid();
        public Guid Reader { get; } = Guid.NewGuid();

        public Guid OwnItem { get; private set; }
        public Guid OtherItem { get; private set; }

        private Host(
            WebApplication app,
            SqliteConnection connection)
        {
            App = app;
            _connection = connection;
            Client = app.GetTestClient();
        }

        public static async Task<Host> Create()
        {
            var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    EnvironmentName = "Testing"
                });

            builder.WebHost.UseTestServer();

            builder.Services.AddDbContext<ApplicationDbContext>(
                options => options.UseSqlite(connection));

            builder.Services
                .AddAuthentication(
                    OpenIddictValidationAspNetCoreDefaults
                        .AuthenticationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme)
                .AddScheme<
                    AuthenticationSchemeOptions,
                    TestAuth>(
                    OpenIddictValidationAspNetCoreDefaults
                        .AuthenticationScheme,
                    _ => { });

            builder.Services
                .AddIdentityCore<ApplicationUser>()
                .AddSignInManager()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddApplication();

            builder.Services
                .AddControllers()
                .AddApplicationPart(
                    typeof(ReusableAccess.Api.Controllers
                        .AdminScopesController).Assembly);

            builder.Services.AddScoped<
                IScopeReadStore,
                ScopeReadStore>();

            builder.Services.AddScoped<IScopeRepository, ScopeRepository>();

            
            builder.Services.AddCompletionInfrastructure();
            builder.Services.AddProblemDetails();

            // همان ثبت‌های احراز هویت و مجوزدهی API واقعی:
            // این extension، PermissionAuthorizationHandler،
            // IHttpContextAccessor و Policyهای Permission را ثبت می‌کند.
            builder.Services.AddReusableAccessAuthorization();

            // Policy و Handler مربوط به PlatformAdmin.
            builder.Services.AddReusableAccessApiAuthorization();

            // Policyهای Completion، از جمله workitems.update.
            builder.Services.AddCompletionApi();

            var cleanup = builder.Services.FirstOrDefault(service =>
                service.ServiceType == typeof(IHostedService) &&
                service.ImplementationType == typeof(ChallengeCleanup));

            if (cleanup is not null)
            {
                builder.Services.Remove(cleanup);
            }

            var app = builder.Build();

            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseCompletionAccountGate();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.UseAntiforgery();

            // MapControllers مکمل endpointهای Completion است
            // و نباید جایگزین آن شود.
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

                var scopeA = new AccessScope("A", "A");
                var scopeB = new AccessScope("B", "B");

                var role = new Role("TEST_EDITOR", "Editor");
                var read = new Permission(
                    "workitems.read",
                    "read");
                var update = new Permission(
                    "workitems.update",
                    "update");

                // پیش از اولین ذخیره، مجوزها را به Role وصل می‌کنیم.
                role.AddPermission(read);
                role.AddPermission(update);

                db.AddRange(
                    scopeA,
                    scopeB,
                    role,
                    read,
                    update);

                await db.SaveChangesAsync();

                db.Add(new UserAccessBundle(
                    host.Reader,
                    "A only",
                    [role.Id],
                    ScopeSelectionMode.Selected,
                    [scopeA.Id]));

                var ownItem = new WorkItem(
                    scopeA.Id,
                    "Own scope item",
                    "Open");

                var otherItem = new WorkItem(
                    scopeB.Id,
                    "Other scope item",
                    "Open");

                host.OwnItem = ownItem.Id;
                host.OtherItem = otherItem.Id;

                db.AddRange(ownItem, otherItem);

                await db.SaveChangesAsync();

                Assert.True(
                    await db.Set<RolePermission>().AnyAsync(
                        item => item.RoleId == role.Id &&
                                item.PermissionId == update.Id));

                Assert.True(
                    await db.Set<UserAccessBundleRole>().AnyAsync(
                        item => item.RoleId == role.Id));

                Assert.True(
                    await db.Set<UserAccessBundleScope>().AnyAsync(
                        item => item.ScopeId == scopeA.Id));
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