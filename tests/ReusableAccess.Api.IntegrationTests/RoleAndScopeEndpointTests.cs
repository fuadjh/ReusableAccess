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
using ReusableAccess.Application.Roles;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Infrastructure.Persistence;
using ReusableAccess.Infrastructure.Roles;
using ReusableAccess.Infrastructure.Scopes;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class RoleAndScopeEndpointTests
{
    [Fact]
    public async Task Role_post_with_invalid_input_returns_bad_request()
    {
        await using var host = await RoleAndScopeTestHost.CreateAsync();

        using var response = await host.Client.PostAsJsonAsync(
            "/api/admin/roles",
            new
            {
                code = "",
                displayName = "Test role"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "کد نقش و نام نمایشی الزامی هستند؛ حداکثر طول آن‌ها به‌ترتیب ۱۰۰ و ۲۰۰ کاراکتر است.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Role_put_for_unknown_id_returns_not_found()
    {
        await using var host = await RoleAndScopeTestHost.CreateAsync();

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/admin/roles/{Guid.NewGuid()}",
            new { displayName = "Renamed role" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "نقش پیدا نشد.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Scope_put_with_invalid_display_name_returns_bad_request()
    {
        await using var host = await RoleAndScopeTestHost.CreateAsync();

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/admin/scopes/{Guid.NewGuid()}",
            new { displayName = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "نام نمایشی Scope الزامی است و حداکثر ۲۰۰ کاراکتر دارد.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Scope_put_for_unknown_id_returns_not_found()
    {
        await using var host = await RoleAndScopeTestHost.CreateAsync();

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/admin/scopes/{Guid.NewGuid()}",
            new { displayName = "Renamed scope" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Scope پیدا نشد.",
            json.RootElement.GetProperty("message").GetString());
    }
}

internal sealed class RoleAndScopeTestHost : IAsyncDisposable
{
    private const string TestAuthenticationScheme = "RoleAndScopeTest";

    private readonly SqliteConnection _connection;
    private readonly WebApplication _application;

    private RoleAndScopeTestHost(
        SqliteConnection connection,
        WebApplication application,
        HttpClient client)
    {
        _connection = connection;
        _application = application;
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<RoleAndScopeTestHost> CreateAsync()
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
                RoleAndScopeTestAuthenticationHandler>(
                TestAuthenticationScheme,
                _ => { });

        // Policy ساده‌شده فقط برای TestHost؛ policy اصلی دست‌نخورده است.
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "PlatformAdmin",
                policy => policy.RequireAuthenticatedUser());
        });

        builder.Services.AddApplication();

        builder.Services.AddScoped<IRoleReadStore, RoleReadStore>();
        builder.Services.AddScoped<IRoleCommandStore, RoleCommandStore>();
        builder.Services.AddScoped<IScopeReadStore, ScopeReadStore>();
        builder.Services.AddScoped<IScopeCommandStore, ScopeCommandStore>();

        var application = builder.Build();

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.EnsureCreatedAsync();
        }

        application.UseAuthentication();
        application.UseAuthorization();
        application.MapAdminRolesEndpoints();
        application.MapAdminScopesEndpoints();

        await application.StartAsync();

        return new RoleAndScopeTestHost(
            connection,
            application,
            application.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class RoleAndScopeTestAuthenticationHandler(
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