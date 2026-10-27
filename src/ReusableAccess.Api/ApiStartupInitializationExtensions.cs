using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using ReusableAccess.Application.Seeding;
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

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var applicationManager = scope.ServiceProvider
                .GetRequiredService<IOpenIddictApplicationManager>();

            const string clientId = "ReusableAccess.Web";

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                DisplayName = "ReusableAccess Web"
            };

            descriptor.AddResourcePermissions(apiResource);
            descriptor.RedirectUris.Add(
                new Uri("https://localhost:7136/signin-oidc"));

            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.Scopes.Profile);
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.Prefixes.Scope + "access_api");

            descriptor.Requirements.Add(
     OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

            var existingApplication =
                await applicationManager.FindByClientIdAsync(clientId);

            if (existingApplication is null)
            {
                await applicationManager.CreateAsync(descriptor);
            }
            else
            {
                await applicationManager.UpdateAsync(existingApplication, descriptor);
            }

            var scopeManager = scope.ServiceProvider
                .GetRequiredService<IOpenIddictScopeManager>();

            const string apiScopeName = "access_api";

            var existingScope =
                await scopeManager.FindByNameAsync(apiScopeName);

            var scopeDescriptor = new OpenIddictScopeDescriptor
            {
                Name = apiScopeName,
                DisplayName = "ReusableAccess API"
            };

            scopeDescriptor.Resources.Add(apiResource);

            if (existingScope is null)
            {
                await scopeManager.CreateAsync(scopeDescriptor);
            }
            else
            {
                await scopeManager.UpdateAsync(existingScope, scopeDescriptor);
            }
        }
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

            var developmentAccessSeeder = developmentScope.ServiceProvider
    .GetRequiredService<IDevelopmentAccessSeeder>();

            await developmentAccessSeeder.SeedAsync(seededUser.Id);
        }
    }
}