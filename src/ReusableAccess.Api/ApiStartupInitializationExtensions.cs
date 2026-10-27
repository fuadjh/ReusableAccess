using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Api;

public static class ApiStartupInitializationExtensions
{
    public static async Task InitializeReusableAccessAsync(
        this WebApplication app,
        string clientSecret,
        string apiResource)
    {
        await PlatformSuperAdminSeeder.SeedAsync(
            app.Services,
            app.Configuration);

        // کد ثبت/به‌روزرسانی OpenIddict Web client و access_api scope
        // را اینجا نگه دارید.

        if (app.Environment.IsDevelopment())
        {
            var developmentUserName =
                app.Configuration["DevelopmentUser:UserName"];

            if (string.IsNullOrWhiteSpace(developmentUserName))
            {
                throw new InvalidOperationException(
                    "DevelopmentUser:UserName is not configured.");
            }

            await using var developmentScope =
                app.Services.CreateAsyncScope();

            var userManager = developmentScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var seededUser =
                await userManager.FindByNameAsync(developmentUserName);

            if (seededUser is null)
            {
                throw new InvalidOperationException(
                    "The configured development user was not found.");
            }

            await DevelopmentAccessSeeder.SeedAsync(
                developmentScope.ServiceProvider,
                seededUser.Id);
        }
    }
}