using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using ReusableAccess.Api.Authorization;
using ReusableAccess.Api.Endpoints;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using OpenIddict.Abstractions;
namespace ReusableAccess.Api.IntegrationTests;

public sealed class WorkItemsEndpointTests
{
    [Fact]
    public async Task Returns_only_work_items_for_clinic_scope()
    {
        await using var host = await ApiTestHost.CreateAsync();

        using var request = CreateRequest(
            host.AssignedUserId,
            roleCode: "CLINIC_ADMIN",
            scopeCode: "CLINIC_1");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var titles = GetTitles(json);

        Assert.Equal(new[] { "پیگیری معاینه دوره‌ای" }, titles);
    }

    [Fact]
    public async Task Returns_only_work_items_for_hse_scope()
    {
        await using var host = await ApiTestHost.CreateAsync();

        using var request = CreateRequest(
            host.AssignedUserId,
            roleCode: "CLINIC_ADMIN",
            scopeCode: "HSE");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var titles = GetTitles(json);

        Assert.Equal(new[] { "بازرسی تجهیزات ایمنی" }, titles);
    }

    [Fact]
    public async Task Denies_scope_not_assigned_to_user()
    {
        await using var host = await ApiTestHost.CreateAsync();

        using var request = CreateRequest(
            host.AssignedUserId,
            roleCode: "CLINIC_ADMIN",
            scopeCode: "DIAGNOSTICS");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Denies_user_without_workitems_permission()
    {
        await using var host = await ApiTestHost.CreateAsync();

        using var request = CreateRequest(
            host.UserWithoutPermissionId,
            roleCode: "CLINIC_VIEWER",
            scopeCode: "CLINIC_1");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(
        Guid userId,
        string roleCode,
        string scopeCode)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/work-items");

        request.Headers.Add("X-Test-User", userId.ToString());
        request.Headers.Add("X-Active-Role", roleCode);
        request.Headers.Add("X-Active-Scope", scopeCode);

        return request;
    }

    private static string[] GetTitles(JsonDocument json)
    {
        return json.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()!)
            .ToArray();
    }
}

internal sealed class ApiTestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplication _application;

    private ApiTestHost(
        SqliteConnection connection,
        WebApplication application,
        HttpClient client,
        Guid assignedUserId,
        Guid userWithoutPermissionId)
    {
        _connection = connection;
        _application = application;
        Client = client;
        AssignedUserId = assignedUserId;
        UserWithoutPermissionId = userWithoutPermissionId;
    }

    public HttpClient Client { get; }

    public Guid AssignedUserId { get; }

    public Guid UserWithoutPermissionId { get; }

    public static async Task<ApiTestHost> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlite(connection);
            options.UseOpenIddict<Guid>();
        });

        var authenticationScheme =
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = authenticationScheme;
                options.DefaultChallengeScheme = authenticationScheme;
                options.DefaultForbidScheme = authenticationScheme;
            })
            .AddScheme<
                AuthenticationSchemeOptions,
                TestAuthenticationHandler>(
                authenticationScheme,
                _ => { });

        builder.Services.AddReusableAccessAuthorization();

        var application = builder.Build();

        application.UseAuthentication();
        application.UseAuthorization();
        application.MapWorkItemsEndpoint();

        var assignedUserId = Guid.NewGuid();
        var userWithoutPermissionId = Guid.NewGuid();

        await SeedDatabaseAsync(
            application,
            assignedUserId,
            userWithoutPermissionId);

        await application.StartAsync();

        return new ApiTestHost(
            connection,
            application,
            application.GetTestClient(),
            assignedUserId,
            userWithoutPermissionId);
    }

    private static async Task SeedDatabaseAsync(
        WebApplication application,
        Guid assignedUserId,
        Guid userWithoutPermissionId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        var assignedUser = CreateUser(
            assignedUserId,
            "TEST-001",
            "Assigned User");

        var userWithoutPermission = CreateUser(
            userWithoutPermissionId,
            "TEST-002",
            "No Permission User");

        var clinicAdmin = new Role(
            "CLINIC_ADMIN",
            "Clinic administrator");

        var workItemsRead = new Permission(
            "workitems.read",
            "Read work items");

        clinicAdmin.AddPermission(workItemsRead);

        var clinicViewer = new Role(
            "CLINIC_VIEWER",
            "Clinic viewer");

        var clinicScope = new AccessScope(
            "CLINIC_1",
            "Clinic 1");

        var hseScope = new AccessScope(
            "HSE",
            "Health and safety");

        var diagnosticsScope = new AccessScope(
            "DIAGNOSTICS",
            "Diagnostics");

        dbContext.AddRange(
            assignedUser,
            userWithoutPermission,
            clinicAdmin,
            clinicViewer,
            workItemsRead,
            clinicScope,
            hseScope,
            diagnosticsScope,
            new UserRoleAssignment(
                assignedUserId,
                clinicAdmin.Id,
                clinicScope.Id),
            new UserRoleAssignment(
                assignedUserId,
                clinicAdmin.Id,
                hseScope.Id),
            new UserRoleAssignment(
                userWithoutPermissionId,
                clinicViewer.Id,
                clinicScope.Id),
            new WorkItem(
                clinicScope.Id,
                "پیگیری معاینه دوره‌ای",
                "Open"),
            new WorkItem(
                hseScope.Id,
                "بازرسی تجهیزات ایمنی",
                "Open"));

        await dbContext.SaveChangesAsync();
    }

    private static ApplicationUser CreateUser(
        Guid id,
        string nationalId,
        string displayName)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = nationalId,
            NormalizedUserName = nationalId,
            NationalId = nationalId,
            DisplayName = displayName
        };
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class TestAuthenticationHandler(
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
        var rawUserId = Request.Headers["X-Test-User"].ToString();

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim(
    OpenIddictConstants.Claims.Private.Scope,
    "access_api")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}