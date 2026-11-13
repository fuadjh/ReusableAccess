using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application.AccessProfiles;
using ReusableAccess.Infrastructure.AccessProfiles;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure;

using Microsoft.AspNetCore.Identity;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Application.Roles;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Application.Seeding;
using ReusableAccess.Application.Users;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Infrastructure.Bundles;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.RolePermissions;
using ReusableAccess.Infrastructure.Roles;
using ReusableAccess.Infrastructure.Scopes;
using ReusableAccess.Infrastructure.Users;
using ReusableAccess.Infrastructure.WorkItems;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseSqlServer(connectionString)
            .UseOpenIddict<Guid>()
.AddInterceptors(
    sp.GetRequiredService<
        ReusableAccess.Infrastructure.Completion.SecurityAuditInterceptor>()));
        services.AddOpenIddict()
    .AddCore(options =>
    {
        options
            .UseEntityFrameworkCore()
            .UseDbContext<ApplicationDbContext>()
            .ReplaceDefaultEntities<Guid>();
    });

        services

    .AddIdentityCore<ApplicationUser>()
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
        services.AddScoped<IAccessProfilesReadStore, AccessProfilesReadStore>();
        services.AddScoped<IAccessProfilePreferenceStore, AccessProfilePreferenceStore>();
        services.AddScoped<IRoleReadStore, RoleReadStore>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IScopeReadStore, ScopeReadStore>();
        services.AddScoped<IScopeRepository, ScopeRepository>();

        services.AddScoped<IUserBundleRepository, UserBundleRepository>();
        services.AddScoped<IWriteTransactionFactory,EfWriteTransactionFactory>();

        services.AddScoped<IScopeDeletionRepository,ScopeDeletionRepository>();
        services.AddScoped<  IRolePermissionsRepository, RolePermissionsRepository>();
     
    
        services.AddScoped<IRolePermissionsQueryStore,RolePermissionsQueryStore>();
     
        services.AddScoped<IWorkItemsQueryStore, WorkItemsQueryStore>();
        services.AddScoped<IDevelopmentAccessSeeder,DevelopmentAccessSeeder>();

        // Handler جست‌وجوی کاربران به این Store وابسته است.
        services.AddScoped<IUserSearchStore, UserSearchStore>();

        // Handler خواندن بسته‌های کاربر به این Store وابسته است.
        services.AddScoped<IUserBundlesQueryStore, UserBundlesQueryStore>();

        services.AddCompletionInfrastructure();
        return services;
    }
}