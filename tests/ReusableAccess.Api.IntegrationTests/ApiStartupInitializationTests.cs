using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using ReusableAccess.Api;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class ApiStartupInitializationTests
{
    [Fact]
    public async Task Initialization_provisions_web_client_and_api_scope()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options
                .UseSqlite(connection)
                .UseOpenIddict<Guid>();
        });

        builder.Services
            .AddOpenIddict()
            .AddCore(options =>
            {
                options
                    .UseEntityFrameworkCore()
                    .UseDbContext<ApplicationDbContext>()
                    .ReplaceDefaultEntities<Guid>();
            });

        await using var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.EnsureCreatedAsync();
        }

        const string clientSecret = "test-client-secret";
        const string apiResource = "urn:reusableaccess:test-api";

        await app.InitializeReusableAccessAsync(
            clientSecret,
            apiResource);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var applicationManager = scope.ServiceProvider
                .GetRequiredService<IOpenIddictApplicationManager>();

            var scopeManager = scope.ServiceProvider
                .GetRequiredService<IOpenIddictScopeManager>();

            var application = await applicationManager
                .FindByClientIdAsync("ReusableAccess.Web");

            var apiScope = await scopeManager
                .FindByNameAsync("access_api");

            Assert.NotNull(application);
            Assert.NotNull(apiScope);
        }

        // اجرای دوباره باید بدون خطا، رکوردهای موجود را به‌روزرسانی کند.
        await app.InitializeReusableAccessAsync(
            clientSecret,
            apiResource);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var applicationManager = scope.ServiceProvider
                .GetRequiredService<IOpenIddictApplicationManager>();

            var scopeManager = scope.ServiceProvider
                .GetRequiredService<IOpenIddictScopeManager>();

            Assert.NotNull(await applicationManager
                .FindByClientIdAsync("ReusableAccess.Web"));

            Assert.NotNull(await scopeManager
                .FindByNameAsync("access_api"));
        }
    }
}