using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using ReusableAccess.Api.Authorization;
using ReusableAccess.Infrastructure.Authorization;

namespace ReusableAccess.Api;

public static class ApiAuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddReusableAccessApiAuthorization(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<
            IAuthorizationHandler,
            PlatformSuperAdminAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AccessApi", policy =>
            {
                policy
                    .AddAuthenticationSchemes(
                        OpenIddictValidationAspNetCoreDefaults
                            .AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        context.User.HasScope("access_api"));
            });

            options.AddPolicy(
                "Permission:diagnostics.read",
                policy =>
                {
                    policy
                        .AddAuthenticationSchemes(
                            OpenIddictValidationAspNetCoreDefaults
                                .AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireAssertion(context =>
                            context.User.HasScope("access_api"))
                        .AddRequirements(
                            new PermissionRequirement("diagnostics.read"));
                });

            options.AddPolicy(
                "Permission:workitems.read",
                policy =>
                {
                    policy
                        .AddAuthenticationSchemes(
                            OpenIddictValidationAspNetCoreDefaults
                                .AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireAssertion(context =>
                            context.User.HasScope("access_api"))
                        .AddRequirements(
                            new PermissionRequirement("workitems.read"));
                });

            options.AddPolicy("PlatformAdmin", policy =>
            {
                policy
                    .AddAuthenticationSchemes(
                        OpenIddictValidationAspNetCoreDefaults
                            .AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        context.User.HasScope("access_api"))
                    .AddRequirements(
                        new PlatformSuperAdminRequirement());
            });
        });

        return services;
    }
}