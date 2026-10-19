using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using ReusableAccess.Infrastructure.Authorization;

namespace ReusableAccess.Api.Authorization;

public static class ApiAuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddReusableAccessAuthorization(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddScoped<
            IAuthorizationHandler,
            PermissionAuthorizationHandler>();

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
                            new PermissionRequirement(
                                "diagnostics.read"));
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
                            new PermissionRequirement(
                                "workitems.read"));
                });
        });

        return services;
    }
}