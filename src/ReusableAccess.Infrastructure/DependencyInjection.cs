using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application.AccessProfiles;
using ReusableAccess.Infrastructure.AccessProfiles;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure;

using Microsoft.AspNetCore.Identity;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Application.Roles;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Infrastructure.Bundles;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.RolePermissions;
using ReusableAccess.Infrastructure.Roles;
using ReusableAccess.Infrastructure.Scopes;
using ReusableAccess.Infrastructure.WorkItems;

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
        services.AddScoped<IAccessProfilesReadStore, AccessProfilesReadStore>();
        services.AddScoped<IAccessProfilePreferenceStore, AccessProfilePreferenceStore>();
        services.AddScoped<IRoleReadStore, RoleReadStore>();
        services.AddScoped<IRoleCommandStore, RoleCommandStore>();
        services.AddScoped<IScopeReadStore, ScopeReadStore>();
        services.AddScoped<IScopeCommandStore, ScopeCommandStore>();
        services.AddScoped<IUserBundleCommandStore, UserBundleCommandStore>();
        services.AddScoped<    IRolePermissionsQueryStore,    RolePermissionsQueryStore>();
        services.AddScoped<    IRolePermissionsCommandStore,    RolePermissionsCommandStore>();
        services.AddScoped<IWorkItemsQueryStore, WorkItemsQueryStore>();

        return services;
    }
}