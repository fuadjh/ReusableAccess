using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ReusableAccess.Infrastructure.Persistence;

public sealed class ApplicationDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var apiDirectory = FindApiDirectory();

        var environment =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile(
                "appsettings.json",
                optional: true,
                reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{environment}.json",
                optional: true,
                reloadOnChange: false);

        if (string.Equals(
                environment,
                "Development",
                StringComparison.OrdinalIgnoreCase))
        {
            // UserSecretsId از پروژهٔ واقعی API خوانده می‌شود؛
            // نه ConnectionString را ثابت می‌کنیم، نه شناسهٔ secrets را.
            var projectPath = Path.Combine(
                apiDirectory,
                "ReusableAccess.Api.csproj");

            var project = XDocument.Load(projectPath);

            var secretsId = project.Descendants()
                .FirstOrDefault(
                    element => element.Name.LocalName == "UserSecretsId")
                ?.Value
                .Trim();

            if (!string.IsNullOrWhiteSpace(secretsId))
            {
                configurationBuilder.AddUserSecrets(secretsId);
            }
        }

        // برای مثال ConnectionStrings__DefaultConnection
        // می‌تواند مقدار فایل‌ها و User Secrets را override کند.
        configurationBuilder.AddEnvironmentVariables();

        var configuration = configurationBuilder.Build();

        var connectionString =
            configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. " +
                "Configure it in API User Secrets or environment variables.");
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>();

        options
            .UseSqlServer(connectionString)
            .UseOpenIddict<Guid>();

        // در Design-time به سرویس‌های HTTP، Audit interceptor،
        // Bootstrap و Senderها نیاز نداریم.
        return new ApplicationDbContext(options.Options);
    }

    private static string FindApiDirectory()
    {
        var startingDirectories = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var startingDirectory in startingDirectories)
        {
            DirectoryInfo? directory = new(startingDirectory);

            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "src",
                    "ReusableAccess.Api");

                var projectPath = Path.Combine(
                    candidate,
                    "ReusableAccess.Api.csproj");

                if (File.Exists(projectPath))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException(
            "ReusableAccess.Api.csproj was not found. " +
            "Run EF commands from the repository root.");
    }
}