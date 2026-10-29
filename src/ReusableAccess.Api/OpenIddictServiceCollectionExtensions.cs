using OpenIddict.Abstractions;



namespace ReusableAccess.Api;

public static class OpenIddictServiceCollectionExtensions
{
    public static IServiceCollection AddReusableAccessOpenIddict(
        this IServiceCollection services,
        string apiResource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiResource);

        services
            .AddOpenIddict()
           
            .AddServer(options =>
            {
                options
                    .SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.Profile,
                    "access_api");

                options.RegisterResources(apiResource);

                options
                    .AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

                options
                    .UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}