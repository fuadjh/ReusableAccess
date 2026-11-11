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
using ReusableAccess.Api.Controllers;
using ReusableAccess.Application;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Completion;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.IntegrationTests;

// این Fixture فقط authentication را شبیه‌سازی می‌کند؛ policy، handler، Identity و EF واقعی‌اند.
// هر Host اتصال SQLite مستقل دارد؛ این تست‌ها جای آزمون رقابت روی SQL Server را نمی‌گیرند.
internal sealed class UserManagementTestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public WebApplication App { get; }
    public HttpClient Client { get; }
    public Guid AdminId { get; } = Guid.NewGuid();
    public Guid ReaderId { get; } = Guid.NewGuid();

    private UserManagementTestHost(WebApplication app, SqliteConnection connection)
    {
        App = app;
        _connection = connection;
        Client = app.GetTestClient();
    }

    public static async Task<UserManagementTestHost> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        WebApplication? app = null;

        try
        {
            await connection.OpenAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });
            builder.WebHost.UseTestServer();

            builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
                options.UseSqlite(connection).AddInterceptors(
                    services.GetRequiredService<SecurityAuditInterceptor>()));

            builder.Services
                .AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, _ => { });

            builder.Services.AddIdentityCore<ApplicationUser>()
                .AddSignInManager()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddApplication();
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(AdminUsersController).Assembly);
            builder.Services.AddCompletionInfrastructure();
            builder.Services.AddProblemDetails();
            builder.Services.AddReusableAccessAuthorization();
            builder.Services.AddReusableAccessApiAuthorization();
            builder.Services.AddCompletionApi();

            // Cleanup زمان‌دار به موضوع این Fixture مربوط نیست و نباید با ساخت schema مسابقه دهد.
            var cleanup = builder.Services.SingleOrDefault(service =>
                service.ServiceType == typeof(IHostedService) &&
                service.ImplementationType == typeof(ChallengeCleanup));
            if (cleanup is not null)
                builder.Services.Remove(cleanup);

            app = builder.Build();
            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseCompletionAccountGate();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.UseAntiforgery();

            // عمداً هر دو نگاشت فعال‌اند تا تست، route تکراریِ Minimal API + MVC را کشف کند.
            app.MapCompletionEndpoints();
            app.MapControllers();
            await app.StartAsync();

            var host = new UserManagementTestHost(app, connection);
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(
                SeedUser(host.AdminId, "1234567891", "Admin", true),
                SeedUser(host.ReaderId, "9876543210", "Reader", false));
            await db.SaveChangesAsync();
            return host;
        }
        catch
        {
            try
            {
                if (app is not null)
                    await app.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
            throw;
        }
    }

    private static ApplicationUser SeedUser(
        Guid id, string nationalId, string firstName, bool isAdmin) => new()
        {
            Id = id,
            UserName = nationalId,
            NormalizedUserName = nationalId,
            NationalId = nationalId,
            FirstName = firstName,
            LastName = "Test",
            DisplayName = $"{firstName} Test",
            OrganizationalCode = $"SEED-{firstName}",
            Email = $"{firstName.ToLowerInvariant()}@example.test",
            NormalizedEmail = $"{firstName.ToUpperInvariant()}@EXAMPLE.TEST",
            PhoneNumber = "+989123456789",
            IsActive = true,
            IsPlatformSuperAdmin = isAdmin
        };

    public void As(Guid userId, int sessionVersion = 0)
    {
        Client.DefaultRequestHeaders.Remove("X-Test-User");
        Client.DefaultRequestHeaders.Remove("X-Test-Version");
        Client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        Client.DefaultRequestHeaders.Add(
            "X-Test-Version", sessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
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

    private sealed class TestAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"].FirstOrDefault(), out var id))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
            [
                new Claim("sub", id.ToString()),
                new Claim(OpenIddictConstants.Claims.Scope, "access_api"),
                new Claim("ra_version", Request.Headers["X-Test-Version"].FirstOrDefault() ?? "0")
            ], Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}