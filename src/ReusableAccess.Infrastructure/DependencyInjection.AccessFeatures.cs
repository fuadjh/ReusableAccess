using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ReusableAccess.Application.Accounts;
using ReusableAccess.Application.AccessProfiles;
using ReusableAccess.Application.Audit;
using ReusableAccess.Application.AuthenticationSettings;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Application.Completion;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Application.Reports;
using ReusableAccess.Application.Roles;
using ReusableAccess.Application.Users;
using ReusableAccess.Application.WorkItems;

using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;

using ReusableAccess.Infrastructure.Accounts;
using ReusableAccess.Infrastructure.AccessProfiles;
using ReusableAccess.Infrastructure.Audit;
using ReusableAccess.Infrastructure.AuthenticationSettings;
using ReusableAccess.Infrastructure.Bundles;
using ReusableAccess.Infrastructure.Completion;
using ReusableAccess.Infrastructure.Persistence;
using ReusableAccess.Infrastructure.Reports;
using ReusableAccess.Infrastructure.Roles;
using ReusableAccess.Infrastructure.Users;
using ReusableAccess.Infrastructure.WorkItems;

namespace ReusableAccess.Infrastructure;

public static class AccessFeatureDependencyInjection
{
    private sealed class RegistrationMarker
    {
    }

    public static IServiceCollection AddAccessFeaturesInfrastructure(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // فراخوانی مجدد این ماژول نباید HostedService یا Handler تکراری بسازد.
        if (services.Any(
                item => item.ServiceType == typeof(RegistrationMarker)))
        {
            return services;
        }

        services.AddSingleton(new RegistrationMarker());

        services.AddDataProtection();
        services.AddHttpContextAccessor();

        services.TryAddSingleton<IConfiguration>(
            new ConfigurationBuilder().Build());

        services.TryAddSingleton<TimeProvider>(
            TimeProvider.System);

        services.AddScoped<SecurityAuditInterceptor>();

        // مسیرهای قدیمی هنوز در API همین commit مصرف می‌شوند.
        // حذف آن‌ها باید هم‌زمان با انتقال Endpointها انجام شود.
        services.AddScoped<CompletionStore>();

        services.AddScoped<ICompletionStore>(
            provider =>
                provider.GetRequiredService<CompletionStore>());

        services.AddScoped<
            IAccountSessionValidator,
            AccountSessionValidator>();

        services.AddScoped<
            IAccountWorkflow,
            AccountWorkflow>();

        services.AddScoped<
            IAccountMessageSender,
            AccountMessageSender>();

        services.AddSingleton<AccountDeliveryQueue>();

        services.AddSingleton<IRecoveryCodeQueue>(
            provider =>
                provider.GetRequiredService<AccountDeliveryQueue>());

        services.AddHostedService<AccountDeliveryQueue>(
            provider =>
                provider.GetRequiredService<AccountDeliveryQueue>());

        services.AddHostedService<ChallengeCleanup>();

        services.AddHttpClient(
                "AccountDelivery",
                client =>
                    client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(
                () => new HttpClientHandler
                {
                    AllowAutoRedirect = false
                });

        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        });

        services.AddScoped<
            IManagedUserQueries,
            ManagedUserQueries>();

        // دو قرارداد فعلی نوشتن کاربران، یک نمونهٔ Scoped مشترک دارند.
        services.AddScoped<ManagedUserCommandStore>();

        services.AddScoped<IManagedUserCommandStore>(
            provider =>
                provider.GetRequiredService<ManagedUserCommandStore>());

        services.AddScoped<IUserRegistrationStore>(
            provider =>
                provider.GetRequiredService<ManagedUserCommandStore>());

        services.TryAddScoped<
            IWriteTransactionFactory,
            EfWriteTransactionFactory>();

        services.TryAddScoped<
            IAccessProfilePreferenceRepository,
            AccessProfilePreferenceRepository>();

        services.AddScoped<
            IAccountRepository,
            AccountRepository>();

        services.AddScoped<
            IAccountIdentityGateway,
            AccountIdentityGateway>();

        services.AddScoped<
            IAccountSignInGateway,
            AccountSignInGateway>();

        services.AddScoped<
            IAccountCodeProtection,
            AccountCodeProtection>();

        services.AddScoped<
            IUserWriteExecution,
            UserWriteExecution>();

        services.AddScoped<
            IAuthenticationSettingsRepository,
            AuthenticationSettingsRepository>();

        services.AddScoped<
            IRoleDeletionRepository,
            RoleDeletionRepository>();

        services.AddScoped<
            IUserAccessAssignmentRepository,
            UserAccessAssignmentRepository>();

        services.AddScoped<
            IAuditQueries,
            AuditQueries>();

        services.AddScoped<
            IUserAccessListQueries,
            UserAccessListQueries>();

        services.AddScoped<
            IWorkItemRepository,
            WorkItemRepository>();

        services.AddScoped<
            IWorkItemScopeQueries,
            WorkItemScopeQueries>();

        services.AddScoped<
            IWorkItemListQueries,
            WorkItemListQueries>();

        services.AddScoped<
            IReportSummaryQueries,
            ReportSummaryQueries>();

        // بعضی Fixtureها AddInfrastructure را صدا نمی‌زنند.
        // TryAdd از ثبت مجدد Handler پس از AddApplication جلوگیری می‌کند.
        var handlers = new[]
        {
            typeof(CompletionHandlers),

            typeof(GetAuthenticationSettingsQueryHandler),
            typeof(UpdateAuthenticationSettingsCommandHandler),
            typeof(DeleteAccessEntityCommandHandler),
            typeof(GetAuditPageQueryHandler),
            typeof(ChangeOwnPasswordCommandHandler),
            typeof(GetUserAccessListQueryHandler),
            typeof(AssignUserAccessCommandHandler),
            typeof(SaveWorkItemCommandHandler),
            typeof(DeleteWorkItemCommandHandler),
            typeof(GetWorkItemListQueryHandler),
            typeof(GetWritableScopesQueryHandler),
            typeof(GetReportSummaryQueryHandler)
        };

        foreach (var handler in handlers)
        {
            var contracts = handler.GetInterfaces()
                .Where(type =>
                    type.IsGenericType &&
                    type.GetGenericTypeDefinition() ==
                    typeof(IRequestHandler<,>));

            foreach (var contract in contracts)
            {
                services.TryAddTransient(
                    contract,
                    handler);
            }
        }

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(CompletionConcurrencyBehavior<,>));

        // وابستگی مشترک Handlerهای ساخت/ویرایش و حذف WorkItem.
        services.AddScoped<IWorkItemRepository, WorkItemRepository>();

        // خواندن محدوده و فهرست WorkItem.
        services.AddScoped<IWorkItemScopeQueries, WorkItemScopeQueries>();
        services.AddScoped<IWorkItemListQueries, WorkItemListQueries>();


        return services;
    }

    // نام عمومی این عملیات فعلاً حفظ می‌شود تا Startup بی‌دلیل تغییر نکند.
    public static async Task SeedCompletionCatalogAsync(
        this IServiceProvider provider,
        CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        foreach (var item in PermissionCatalog.All)
        {
            var exists = await db.Set<Permission>()
                .AnyAsync(
                    permission => permission.Key == item.Key,
                    ct);

            if (!exists)
            {
                db.Add(new Permission(
                    item.Key,
                    item.Value));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}