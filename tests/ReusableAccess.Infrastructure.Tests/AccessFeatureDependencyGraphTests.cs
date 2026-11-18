using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReusableAccess.Application;
using ReusableAccess.Application.AccessProfiles;
using ReusableAccess.Application.Accounts;
using ReusableAccess.Application.Audit;
using ReusableAccess.Application.AuthenticationSettings;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Application.Reports;
using ReusableAccess.Application.RolePermissions;
using ReusableAccess.Application.Roles;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Application.Users;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Contracts;
using ReusableAccess.Infrastructure.AccessProfiles;
using ReusableAccess.Infrastructure.Accounts;
using ReusableAccess.Infrastructure.Bundles;
using ReusableAccess.Infrastructure.Persistence;
using ReusableAccess.Infrastructure.RolePermissions;
using ReusableAccess.Infrastructure.Roles;
using ReusableAccess.Infrastructure.Scopes;
using ReusableAccess.Infrastructure.Users;
using ReusableAccess.Infrastructure.WorkItems;
using Xunit;
using Assert = Xunit.Assert;

namespace ReusableAccess.Infrastructure.Tests;

public sealed class AccessFeatureDependencyGraphTests
{
    [Fact]
    public async Task Feature_dependencies_resolve_without_query_execution()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddAuthentication()
            .AddCookie(IdentityConstants.ApplicationScheme);

        services.AddApplication();

        // فقط برای ساخت DbContext و گراف وابستگی است.
        // هیچ Query، CanConnect یا Migration اجرا نمی‌شود.
        services.AddInfrastructure(
            "Server=127.0.0.1,1;" +
            "Database=DependencyGraphOnly;" +
            "Integrated Security=True;" +
            "TrustServerCertificate=True;" +
            "Connect Timeout=1");

        // تست گراف نباید کلید دائمی Data Protection بسازد.
        services.AddSingleton<IDataProtectionProvider>(
            new EphemeralDataProtectionProvider());

        // ثبت دوباره نباید Handler یا Session Validator موازی بسازد.
        services.AddAccessFeaturesInfrastructure();
        services.AddAccessFeaturesInfrastructure();

        await using var root = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });

        await using var scope = root.CreateAsyncScope();

        var provider = scope.ServiceProvider;

        var pairs = new (Type Request, Type Response)[]
        {
            (
                typeof(GetAuthenticationSettingsQuery),
                typeof(SettingsView)
            ),
            (
                typeof(UpdateAuthenticationSettingsCommand),
                typeof(bool)
            ),
            (
                typeof(DeleteAccessEntityCommand),
                typeof(bool)
            ),
            (
                typeof(GetAuditPageQuery),
                typeof(PageResult<AuditRow>)
            ),
            (
                typeof(ChangeOwnPasswordCommand),
                typeof(bool)
            ),
            (
                typeof(GetUserAccessListQuery),
                typeof(IReadOnlyList<BundleRow>)
            ),
            (
                typeof(AssignUserAccessCommand),
                typeof(Guid)
            ),
            (
                typeof(SaveWorkItemCommand),
                typeof(Guid)
            ),
            (
                typeof(DeleteWorkItemCommand),
                typeof(bool)
            ),
            (
                typeof(GetWorkItemListQuery),
                typeof(IReadOnlyList<WorkItemRow>)
            ),
            (
                typeof(GetWritableScopesQuery),
                typeof(IReadOnlyList<ScopeRow>)
            ),
            (
                typeof(GetReportSummaryQuery),
                typeof(ReportSummary)
            )
        };

        foreach (var pair in pairs)
        {
            var handlerContract = typeof(IRequestHandler<,>)
                .MakeGenericType(
                    pair.Request,
                    pair.Response);

            Assert.Single(
                provider.GetServices(handlerContract));
        }

        Assert.NotNull(
            provider.GetRequiredService<IAccessProfileQueries>());

        Assert.NotNull(
            provider.GetRequiredService<IRoleQueries>());

        Assert.NotNull(
            provider.GetRequiredService<IScopeQueries>());

        Assert.NotNull(
            provider.GetRequiredService<IRolePermissionsQueries>());

        Assert.NotNull(
            provider.GetRequiredService<IWorkItemsQueries>());

        Assert.NotNull(
            provider.GetRequiredService<IUserSearchQueries>());

        Assert.NotNull(
            provider.GetRequiredService<IUserBundleQueries>());

        Assert.NotNull(
            provider.GetRequiredService<IManagedUserQueries>());

        Assert.NotNull(
            provider.GetRequiredService<IAccountWorkflow>());

        Assert.NotNull(
            provider.GetRequiredService<IAccountMessageSender>());

        Assert.IsType<AccountSessionValidator>(
            Assert.Single(
                provider.GetServices<IAccountSessionValidator>()));
    }

    [Fact]
    public async Task Independent_profile_composition_needs_no_hosted_services()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();

        services.AddDbContext<ApplicationDbContext>(
            options =>
                options.UseSqlite("Data Source=:memory:"));

        services.AddScoped<
            IAccountRepository,
            AccountRepository>();

        services.AddScoped<
            IAccessProfilePreferenceRepository,
            AccessProfilePreferenceRepository>();

        await using var root = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });

        await using var scope = root.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<
                SetAccessProfilePreferenceCommand,
                AccessProfilePreferenceSelection?>>();

        Assert.NotNull(handler);

        Assert.Empty(
            services.Where(
                item =>
                    item.ServiceType == typeof(IHostedService)));
    }

    [Theory]
    [InlineData("IAccountSessionValidator")]
    [InlineData("IAccountWorkflow")]
    [InlineData("IAccountMessageSender")]
    [InlineData("IRecoveryCodeQueue")]
    public void Account_contracts_have_one_canonical_namespace(
        string name)
    {
        var assembly = typeof(IAccountMessageSender).Assembly;

        Assert.NotNull(
            assembly.GetType(
                "ReusableAccess.Application.Accounts." + name));

        Assert.Null(
            assembly.GetType(
                "ReusableAccess.Application.Completion." + name));
    }
}