using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure;

using Microsoft.AspNetCore.Identity;
using ReusableAccess.Infrastructure.Identity;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString)
            .UseOpenIddict<Guid>());
        services

    .AddIdentityCore<ApplicationUser>()
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

        return services;
    }
}