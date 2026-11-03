using System.Security.Cryptography.X509Certificates;
using OpenIddict.Abstractions;

namespace ReusableAccess.Api;

public static class OpenIddictServiceCollectionExtensions
{
    // برای TestHostهای مستقل موجود حفظ می‌شود.
    public static IServiceCollection AddReusableAccessOpenIddict(
        this IServiceCollection services, string apiResource)
        => Configure(services, apiResource, null, null);

    public static IServiceCollection AddReusableAccessOpenIddict(
        this IServiceCollection services,
        string apiResource,
        IConfiguration configuration,
        IHostEnvironment environment)
        => Configure(services, apiResource, configuration, environment);

    private static IServiceCollection Configure(
        IServiceCollection services,
        string apiResource,
        IConfiguration? config,
        IHostEnvironment? environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiResource);

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetEndSessionEndpointUris("/connect/logout")
                    .AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.Profile, "access_api");

                options.RegisterResources(apiResource);

                if (environment is null || environment.IsDevelopment())
                {
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    X509Certificate2 Load(string kind)
                    {
                        var path = config![$"Certificates:{kind}:Path"]
                            ?? throw new InvalidOperationException(
                                $"Missing {kind} certificate path.");

                        var cert = X509CertificateLoader.LoadPkcs12FromFile(
                            path,
                            config[$"Certificates:{kind}:Password"],
                            X509KeyStorageFlags.EphemeralKeySet);

                        if (!cert.HasPrivateKey ||
                            cert.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
                            cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                        {
                            throw new InvalidOperationException(
                                $"Invalid {kind} certificate.");
                        }

                        return cert;
                    }

                    options.AddEncryptionCertificate(Load("Encryption"))
                        .AddSigningCertificate(Load("Signing"));
                }

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}