using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Completion;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure;

public static class CompletionServiceCollectionExtensions
{
    public static IServiceCollection AddCompletionInfrastructure(
        this IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddHttpContextAccessor();

        services.TryAddSingleton<IConfiguration>(
            new ConfigurationBuilder().Build());

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        services.AddScoped<SecurityAuditInterceptor>();
        services.AddScoped<CompletionStore>();

        services.AddScoped<ICompletionStore>(
            s => s.GetRequiredService<CompletionStore>());

        services.AddScoped<IAccountSessionValidator>(
            s => s.GetRequiredService<CompletionStore>());

        services.AddScoped<IAccountWorkflow, AccountWorkflow>();
        services.AddScoped<IAccountMessageSender, AccountMessageSender>();

        services.AddSingleton<AccountDeliveryQueue>();
        services.AddSingleton<IRecoveryCodeQueue>(
            s => s.GetRequiredService<AccountDeliveryQueue>());

        services.AddHostedService<AccountDeliveryQueue>(
            s => s.GetRequiredService<AccountDeliveryQueue>());

        services.AddHostedService<ChallengeCleanup>();

        services.AddHttpClient(
            "AccountDelivery", c => c.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false });

        services.Configure<IdentityOptions>(o =>
        {
            o.Password.RequiredLength = 12;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireUppercase = true;
            o.Password.RequireNonAlphanumeric = true;

            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.Lockout.AllowedForNewUsers = true;

            o.User.RequireUniqueEmail = true;
        });

        foreach (var contract in typeof(CompletionHandlers).GetInterfaces()
                     .Where(x => x.IsGenericType &&
                         x.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
        {
            services.TryAddTransient(contract, typeof(CompletionHandlers));
        }

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(CompletionConcurrencyBehavior<,>));

        return services;
    }

    public static async Task SeedCompletionCatalogAsync(
        this IServiceProvider provider, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        foreach (var item in PermissionCatalog.All)
        {
            if (!await db.Set<Permission>().AnyAsync(
                    p => p.Key == item.Key, ct))
            {
                db.Add(new Permission(item.Key, item.Value));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}