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
using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;
using ReusableAccess.Infrastructure.RolePermissions;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class RolePermissionsEndpointTests
{
    [Fact]
    public async Task Get_returns_not_found_for_unknown_role()
    {
        await using var host = await RolePermissionsTestHost.CreateAsync();

        using var response = await host.Client.GetAsync(
            $"/api/admin/roles/{Guid.NewGuid()}/permissions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "نقش پیدا نشد.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Get_returns_permission_data_with_selected_state_and_category()
    {
        await using var host = await RolePermissionsTestHost.CreateAsync();

        using var response = await host.Client.GetAsync(
            $"/api/admin/roles/{host.RoleId}/permissions?q=roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;
        Assert.Equal(
            "فهرست مجوزهای نقش.",
            root.GetProperty("message").GetString());

        var data = root.GetProperty("data").EnumerateArray().ToArray();

        var permission = Assert.Single(data);
        Assert.Equal(
            "roles.write",
            permission.GetProperty("key").GetString());
        Assert.Equal(
            "roles",
            permission.GetProperty("category").GetString());
        Assert.False(permission.GetProperty("isSelected").GetBoolean());
        Assert.False(
            string.IsNullOrWhiteSpace(
                permission.GetProperty("permissionId").GetString()));
    }

    [Fact]
    public async Task Put_validates_payload_before_role_lookup()
    {
        await using var host = await RolePermissionsTestHost.CreateAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/admin/roles/{Guid.NewGuid()}/permissions")
        {
            Content = JsonContent.Create(
                new { permissionKeys = (string[]?)null })
        };

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "permissionKeys باید آرایه‌ای از کلیدهای معتبر باشد.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Put_returns_unknown_permission_keys()
    {
        await using var host = await RolePermissionsTestHost.CreateAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/admin/roles/{host.RoleId}/permissions")
        {
            Content = JsonContent.Create(
                new { permissionKeys = new[] { "missing.permission" } })
        };

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;
        Assert.Equal(
            "یک یا چند Permission ناشناخته است.",
            root.GetProperty("message").GetString());

        Assert.Equal(
            new[] { "missing.permission" },
            root.GetProperty("unknownPermissionKeys")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray());
    }

    [Fact]
    public async Task Put_replaces_permissions_and_returns_sorted_keys()
    {
        await using var host = await RolePermissionsTestHost.CreateAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/admin/roles/{host.RoleId}/permissions")
        {
            Content = JsonContent.Create(
                new
                {
                    permissionKeys = new[]
                    {
                        "users.read",
                        "roles.write"
                    }
                })
        };

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;
        Assert.Equal(
            "مجوزهای نقش به‌روزرسانی شد.",
            root.GetProperty("message").GetString());

        var data = root.GetProperty("data");
        Assert.Equal(
            host.RoleId.ToString(),
            data.GetProperty("roleId").GetString());

        Assert.Equal(
            new[] { "roles.write", "users.read" },
            data.GetProperty("permissionKeys")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray());
    }
}

internal sealed class RolePermissionsTestHost : IAsyncDisposable
{
    private const string TestAuthenticationScheme = "RolePermissionsTest";

    private readonly SqliteConnection _connection;
    private readonly WebApplication _application;

    private RolePermissionsTestHost(
        SqliteConnection connection,
        WebApplication application,
        HttpClient client,
        Guid roleId)
    {
        _connection = connection;
        _application = application;
        Client = client;
        RoleId = roleId;
    }

    public HttpClient Client { get; }

    public Guid RoleId { get; }

    public static async Task<RolePermissionsTestHost> CreateAsync()
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
                RolePermissionsTestAuthenticationHandler>(
                TestAuthenticationScheme,
                _ => { });

        // فقط policy تستی است؛ policy محیط واقعی تغییر نمی‌کند.
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "PlatformAdmin",
                policy => policy.RequireAuthenticatedUser());
        });

        builder.Services.AddApplication();

        builder.Services.AddScoped<
            IRolePermissionsQueryStore,
            RolePermissionsQueryStore>();

        builder.Services.AddScoped<
            IRolePermissionsCommandStore,
            RolePermissionsCommandStore>();

        var application = builder.Build();
        var roleId = Guid.NewGuid();

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.EnsureCreatedAsync();

            var role = new Role("TEST_ADMIN", "Test administrator");
            var selectedPermission =
                new Permission("users.read", "Read users");
            var otherPermission =
                new Permission("roles.write", "Write roles");

            role.AddPermission(selectedPermission);

            dbContext.AddRange(role, selectedPermission, otherPermission);
            await dbContext.SaveChangesAsync();

            roleId = role.Id;
        }

        application.UseAuthentication();
        application.UseAuthorization();
        application.MapAdminRolePermissionsEndpoint();

        await application.StartAsync();

        return new RolePermissionsTestHost(
            connection,
            application,
            application.GetTestClient(),
            roleId);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class RolePermissionsTestAuthenticationHandler(
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