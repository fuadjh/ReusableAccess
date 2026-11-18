using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReusableAccess.Application.AccessProfiles;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Application.Roles;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Application.Seeding;
using ReusableAccess.Application.Users;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Infrastructure.AccessProfiles;
using ReusableAccess.Infrastructure.Bundles;
using ReusableAccess.Infrastructure.Completion;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

using ReusableAccess.Infrastructure.RolePermissions;
using ReusableAccess.Infrastructure.Roles;
using ReusableAccess.Infrastructure.Scopes;
using ReusableAccess.Infrastructure.Users;
using ReusableAccess.Infrastructure.WorkItems;
namespace ReusableAccess.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString);
            options.UseOpenIddict<Guid>();
            options.AddInterceptors(provider.GetRequiredService<SecurityAuditInterceptor>());
        });
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore()
            .UseDbContext<ApplicationDbContext>().ReplaceDefaultEntities<Guid>());
        services.AddIdentityCore<ApplicationUser>().AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddScoped<IAccessProfileQueries, AccessProfileQueries>();
        services.AddScoped<IAccessProfilePreferenceRepository, AccessProfilePreferenceRepository>();
        services.AddScoped<IRoleQueries, RoleQueries>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IScopeQueries, ScopeQueries>();
        services.AddScoped<IScopeRepository, ScopeRepository>();
        services.AddScoped<IUserBundleRepository, UserBundleRepository>();
        services.AddScoped<IWriteTransactionFactory, EfWriteTransactionFactory>();
        services.AddScoped<IScopeDeletionRepository, ScopeDeletionRepository>();
        services.AddScoped<IRolePermissionsRepository, RolePermissionsRepository>();
        services.AddScoped<IRolePermissionsQueries, RolePermissionsQueries>();
        services.AddScoped<IWorkItemsQueries, WorkItemsQueries>();
        services.AddScoped<IDevelopmentAccessSeeder, DevelopmentAccessSeeder>();
        services.AddScoped<IUserSearchQueries, UserSearchQueries>();
        services.AddScoped<IUserBundleQueries, UserBundleQueries>();
        services.AddAccessFeaturesInfrastructure();

        // اگر ماژول قابلیت‌ها این قرارداد را ثبت نکرده باشد، اینجا ثبت می‌شود.
        // TryAddScoped در صورت وجود ثبت قبلی، ثبت دیگری اضافه نمی‌کند.
        

        return services;
       
    }
}
