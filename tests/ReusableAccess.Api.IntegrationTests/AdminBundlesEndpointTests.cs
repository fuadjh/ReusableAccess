using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReusableAccess.Api.Endpoints;
using ReusableAccess.Application;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Infrastructure.Bundles;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class AdminBundlesEndpointTests
{
    [Fact]
    public async Task User_search_requires_at_least_two_characters()
    {
        await using var host = await AdminBundlesTestHost.CreateAsync();

        using var response = await host.Client.GetAsync(
            "/api/admin/users?q=a");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "برای جست‌وجوی کاربر حداقل دو حرف وارد کنید.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Create_returns_not_found_when_user_does_not_exist()
    {
        await using var host = await AdminBundlesTestHost.CreateAsync();

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/admin/users/{Guid.NewGuid()}/bundles",
            new
            {
                name = "Valid bundle",
                roleIds = new[] { Guid.NewGuid() },
                scopeMode = "selected",
                scopeIds = new[] { Guid.NewGuid() },
                sortOrder = 0,
                isEnabled = true
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "کاربر پیدا نشد.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Create_returns_bad_request_for_invalid_bundle_definition()
    {
        await using var host = await AdminBundlesTestHost.CreateAsync();

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/admin/users/{host.UserId}/bundles",
            new
            {
                name = " ",
                roleIds = new[] { Guid.NewGuid() },
                scopeMode = "selected",
                scopeIds = new[] { Guid.NewGuid() },
                sortOrder = 0,
                isEnabled = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "اطلاعات Bundle معتبر نیست.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Update_returns_not_found_for_unknown_bundle()
    {
        await using var host = await AdminBundlesTestHost.CreateAsync();

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/admin/bundles/{Guid.NewGuid()}",
            new
            {
                name = "Valid bundle",
                roleIds = new[] { Guid.NewGuid() },
                scopeMode = "selected",
                scopeIds = new[] { Guid.NewGuid() },
                sortOrder = 0,
                isEnabled = true
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Bundle پیدا نشد.",
            json.RootElement.GetProperty("message").GetString());
    }
}

internal sealed class AdminBundlesTestHost : IAsyncDisposable
{
    private const string TestAuthenticationScheme = "AdminBundlesTest";

    private readonly SqliteConnection _connection;
    private readonly WebApplication _application;

    private AdminBundlesTestHost(
        SqliteConnection connection,
        WebApplication application,
        HttpClient client,
        Guid userId)
    {
        _connection = connection;
        _application = application;
        Client = client;
        UserId = userId;
    }

    public HttpClient Client { get; }

    public Guid UserId { get; }

    public static async Task<AdminBundlesTestHost> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options
                .UseSqlite(connection)
                .UseOpenIddict<Guid>();
        });

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    TestAuthenticationScheme;
                options.DefaultChallengeScheme =
                    TestAuthenticationScheme;
            })
            .AddScheme<
                AuthenticationSchemeOptions,
                AdminBundlesTestAuthenticationHandler>(
                TestAuthenticationScheme,
                _ => { });

        // Policy ساده‌شده فقط برای TestHost.
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "PlatformAdmin",
                policy => policy.RequireAuthenticatedUser());
        });

        builder.Services.AddApplication();

        builder.Services.AddScoped<
            IUserBundleCommandStore,
            UserBundleCommandStore>();

        var application = builder.Build();
        var userId = Guid.NewGuid();

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.EnsureCreatedAsync();

            dbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = "bundle-test-user",
                NormalizedUserName = "BUNDLE-TEST-USER",
                NationalId = $"TEST-{userId:N}",
                DisplayName = "Bundle test user"
            });

            await dbContext.SaveChangesAsync();
        }

        application.UseAuthentication();
        application.UseAuthorization();
        application.MapAdminBundlesEndpoints();

        await application.StartAsync();

        return new AdminBundlesTestHost(
            connection,
            application,
            application.GetTestClient(),
            userId);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class AdminBundlesTestAuthenticationHandler(
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
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, "integration-test-admin")
            },
            Scheme.Name);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}