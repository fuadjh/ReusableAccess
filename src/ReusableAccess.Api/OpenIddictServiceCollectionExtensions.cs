using System.Security.Cryptography.X509Certificates;
using OpenIddict.Abstractions;

namespace ReusableAccess.Api;

public static class OpenIddictServiceCollectionExtensions
{
    private sealed class RegistrationMarker { }

    public static IServiceCollection AddReusableAccessOpenIddict(
        this IServiceCollection services,
        string apiResource,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiResource);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (services.Any(d => d.ServiceType == typeof(RegistrationMarker)))
        {
            throw new InvalidOperationException(
                "ReusableAccess OpenIddict is already registered.");
        }

        services.AddSingleton(new RegistrationMarker());

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

                if (environment.IsDevelopment())
                {
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    options.AddEncryptionCertificate(
                        LoadCertificate(configuration, "Encryption"));

                    options.AddSigningCertificate(
                        LoadCertificate(configuration, "Signing"));
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

    private static X509Certificate2 LoadCertificate(
        IConfiguration configuration, string kind)
    {
        var path = configuration[$"Certificates:{kind}:Path"];

        // عملگر ?? رشتهٔ خالی یا فقط فاصله را رد نمی‌کند.
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                $"Missing {kind} certificate path.");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            path,
            configuration[$"Certificates:{kind}:Password"],
            X509KeyStorageFlags.EphemeralKeySet);

        var now = DateTime.UtcNow;
        if (!certificate.HasPrivateKey ||
            certificate.NotBefore.ToUniversalTime() > now ||
            certificate.NotAfter.ToUniversalTime() <= now)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                $"Invalid {kind} certificate.");
        }

        return certificate;
    }
}