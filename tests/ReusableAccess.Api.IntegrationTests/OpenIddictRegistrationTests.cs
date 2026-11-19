using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using ReusableAccess.Api;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class OpenIddictRegistrationTests
{
    private const string Resource = "urn:reusableaccess:test-api";
    private const string PfxPassword = "temporary-test-password";

    [Fact]
    public void Production_registers_only_the_configured_certificates()
    {
        var dir = Directory.CreateTempSubdirectory("ra-oidc-");

        try
        {
            var builder = CreateProductionBuilder();

            var encryption = ConfigureCertificate(
                builder, dir, "Encryption", X509KeyUsageFlags.KeyEncipherment);

            var signing = ConfigureCertificate(
                builder, dir, "Signing", X509KeyUsageFlags.DigitalSignature);

            Register(builder);

            using var provider = builder.Services.BuildServiceProvider();
            var options = GetServerOptions(provider);

            var encryptionKey = Assert.IsType<X509SecurityKey>(
                Assert.Single(options.EncryptionCredentials).Key);

            var signingKey = Assert.IsType<X509SecurityKey>(
                Assert.Single(options.SigningCredentials).Key);

            Assert.Equal(encryption, encryptionKey.Certificate.Thumbprint);
            Assert.Equal(signing, signingKey.Certificate.Thumbprint);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("Encryption")]
    [InlineData("Signing")]
    public void Production_fails_when_a_certificate_path_is_missing(string kind)
    {
        var dir = Directory.CreateTempSubdirectory("ra-oidc-");

        try
        {
            var builder = CreateProductionBuilder();

            ConfigureCertificate(
                builder, dir, "Encryption", X509KeyUsageFlags.KeyEncipherment);

            ConfigureCertificate(
                builder, dir, "Signing", X509KeyUsageFlags.DigitalSignature);

            builder.Configuration[$"Certificates:{kind}:Path"] = "";

            var error = Assert.Throws<InvalidOperationException>(
                () => Register(builder));

            Assert.Equal($"Missing {kind} certificate path.", error.Message);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Duplicate_registration_is_rejected()
    {
        var dir = Directory.CreateTempSubdirectory("ra-oidc-");

        try
        {
            var builder = CreateProductionBuilder();

            ConfigureCertificate(
                builder, dir, "Encryption", X509KeyUsageFlags.KeyEncipherment);

            ConfigureCertificate(
                builder, dir, "Signing", X509KeyUsageFlags.DigitalSignature);

            Register(builder); // ثبت اول باید موفق شود.

            var error = Assert.Throws<InvalidOperationException>(
                () => Register(builder));

            Assert.Equal(
                "ReusableAccess OpenIddict is already registered.",
                error.Message);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static WebApplicationBuilder CreateProductionBuilder() =>
        WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

    private static void Register(WebApplicationBuilder builder) =>
        builder.Services.AddReusableAccessOpenIddict(
            Resource, builder.Configuration, builder.Environment);

    private static OpenIddictServerOptions GetServerOptions(
        IServiceProvider provider) =>
        provider.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>()
            .CurrentValue;

    private static string ConfigureCertificate(
        WebApplicationBuilder builder,
        DirectoryInfo dir,
        string kind,
        X509KeyUsageFlags usage)
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            $"CN=ReusableAccess Test {kind}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(usage, critical: true));

        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(
            now.AddDays(-1), now.AddDays(1));

        var path = Path.Combine(dir.FullName, $"{kind}.pfx");
        File.WriteAllBytes(
            path, certificate.Export(X509ContentType.Pkcs12, PfxPassword));

        builder.Configuration[$"Certificates:{kind}:Path"] = path;
        builder.Configuration[$"Certificates:{kind}:Password"] = PfxPassword;

        return certificate.Thumbprint;
    }
}