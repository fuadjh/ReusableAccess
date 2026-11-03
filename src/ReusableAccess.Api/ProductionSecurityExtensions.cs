using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace ReusableAccess.Api.Completion;

public static class ProductionSecurityExtensions
{
    public static IServiceCollection AddCompletionDeployment(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment environment)
    {
        var name = config["Deployment:ApplicationName"] ?? "ReusableAccess";

        services.Configure<CookieAuthenticationOptions>(
            IdentityConstants.ApplicationScheme,
            o => o.Cookie.Name = "." +
                string.Concat(name.Where(
                    c => char.IsAsciiLetterOrDigit(c) || c == '-')) +
                ".Identity");

        var dp = services.AddDataProtection()
            .SetApplicationName(name + ".Identity");

        var keys = config["Deployment:DataProtectionKeysPath"];

        if (!string.IsNullOrWhiteSpace(keys))
            dp.PersistKeysToFileSystem(new DirectoryInfo(keys));

        if (!environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(keys))
                throw new InvalidOperationException(
                    "Configure Deployment:DataProtectionKeysPath.");

            if (!Uri.TryCreate(
                    config["Hosting:WebOrigin"], UriKind.Absolute, out var web) ||
                web.Scheme != "https")
                throw new InvalidOperationException(
                    "Hosting:WebOrigin must be HTTPS.");

            var path = config["Certificates:DataProtection:Path"]
                ?? throw new InvalidOperationException(
                    "Configure the Data Protection certificate.");

            var cert = X509CertificateLoader.LoadPkcs12FromFile(
                path, config["Certificates:DataProtection:Password"],
                X509KeyStorageFlags.EphemeralKeySet);

            if (!cert.HasPrivateKey)
                throw new InvalidOperationException(
                    "Data Protection certificate must have a private key.");

            dp.ProtectKeysWithCertificate(cert);
        }

        return services;
    }
}