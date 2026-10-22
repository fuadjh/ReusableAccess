using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    private const string ActiveRoleHeader = "X-Active-Role";
    private const string ActiveScopeHeader = "X-Active-Scope";
    private const string ActiveScopeModeHeader = "X-Active-Scope-Mode";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var subject = context.User.FindFirst("sub")?.Value;
        var roleCode =
            httpContext?.Request.Headers[ActiveRoleHeader].ToString();
        var scopeCode =
            httpContext?.Request.Headers[ActiveScopeHeader].ToString();
        var scopeModeValue =
            httpContext?.Request.Headers[ActiveScopeModeHeader].ToString();

        if (httpContext is null ||
            !Guid.TryParse(subject, out var userId) ||
            string.IsNullOrWhiteSpace(roleCode) ||
            !TryParseScopeMode(scopeModeValue, out var scopeMode))
        {
            return;
        }

        if (scopeMode == ScopeSelectionMode.Selected &&
            string.IsNullOrWhiteSpace(scopeCode))
        {
            return;
        }

        var cancellationToken = httpContext.RequestAborted;

        var hasBundlePermission = scopeMode switch
        {
            ScopeSelectionMode.Selected =>
                await HasSelectedScopeBundlePermissionAsync(
                    userId,
                    roleCode,
                    scopeCode!,
                    requirement.PermissionKey,
                    cancellationToken),

            ScopeSelectionMode.All =>
                await HasAllScopesBundlePermissionAsync(
                    userId,
                    roleCode,
                    requirement.PermissionKey,
                    cancellationToken),

            _ => false
        };

        var userHasAnyBundle = await dbContext.Set<UserAccessBundle>()
    .AnyAsync(
        bundle => bundle.UserId == userId,
        cancellationToken);

        // سازگاری موقت با تخصیص‌های قدیمی؛ فقط برای اسکوپ مشخص.
        var hasLegacyPermission =
     !userHasAnyBundle &&
     !hasBundlePermission &&
     scopeMode == ScopeSelectionMode.Selected &&
     await HasLegacyAssignmentPermissionAsync(
         userId,
         roleCode,
         scopeCode!,
         requirement.PermissionKey,
         cancellationToken);

        if (hasBundlePermission || hasLegacyPermission)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> HasSelectedScopeBundlePermissionAsync(
        Guid userId,
        string roleCode,
        string scopeCode,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var query =
            from bundle in dbContext.Set<UserAccessBundle>()
            join bundleRole in dbContext.Set<UserAccessBundleRole>()
                on bundle.Id equals bundleRole.BundleId
            join role in dbContext.Set<Role>()
                on bundleRole.RoleId equals role.Id
            join rolePermission in dbContext.Set<RolePermission>()
                on role.Id equals rolePermission.RoleId
            join permission in dbContext.Set<Permission>()
                on rolePermission.PermissionId equals permission.Id
            join bundleScope in dbContext.Set<UserAccessBundleScope>()
                on bundle.Id equals bundleScope.BundleId
            join scope in dbContext.Set<AccessScope>()
                on bundleScope.ScopeId equals scope.Id
            where bundle.UserId == userId
                  && bundle.IsEnabled
                  && bundle.ScopeSelectionMode == ScopeSelectionMode.Selected
                  && role.Code == roleCode
                  && scope.Code == scopeCode
                  && permission.Key == permissionKey
            select bundle.Id;

        return await query.AnyAsync(cancellationToken);
    }

    private async Task<bool> HasAllScopesBundlePermissionAsync(
        Guid userId,
        string roleCode,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var query =
            from bundle in dbContext.Set<UserAccessBundle>()
            join bundleRole in dbContext.Set<UserAccessBundleRole>()
                on bundle.Id equals bundleRole.BundleId
            join role in dbContext.Set<Role>()
                on bundleRole.RoleId equals role.Id
            join rolePermission in dbContext.Set<RolePermission>()
                on role.Id equals rolePermission.RoleId
            join permission in dbContext.Set<Permission>()
                on rolePermission.PermissionId equals permission.Id
            where bundle.UserId == userId
                  && bundle.IsEnabled
                  && bundle.ScopeSelectionMode == ScopeSelectionMode.All
                  && role.Code == roleCode
                  && permission.Key == permissionKey
            select bundle.Id;

        return await query.AnyAsync(cancellationToken);
    }

    private async Task<bool> HasLegacyAssignmentPermissionAsync(
        Guid userId,
        string roleCode,
        string scopeCode,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var query =
            from assignment in dbContext.Set<UserRoleAssignment>()
            join role in dbContext.Set<Role>()
                on assignment.RoleId equals role.Id
            join scope in dbContext.Set<AccessScope>()
                on assignment.ScopeId equals scope.Id
            join rolePermission in dbContext.Set<RolePermission>()
                on role.Id equals rolePermission.RoleId
            join permission in dbContext.Set<Permission>()
                on rolePermission.PermissionId equals permission.Id
            where assignment.UserId == userId
                  && assignment.IsEnabled
                  && role.Code == roleCode
                  && scope.Code == scopeCode
                  && permission.Key == permissionKey
            select permission.Id;

        return await query.AnyAsync(cancellationToken);
    }

    private static bool TryParseScopeMode(
        string? value,
        out ScopeSelectionMode scopeMode)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(
                value.Trim(),
                "selected",
                StringComparison.OrdinalIgnoreCase))
        {
            scopeMode = ScopeSelectionMode.Selected;
            return true;
        }

        if (string.Equals(
            value.Trim(),
            "all",
            StringComparison.OrdinalIgnoreCase))
        {
            scopeMode = ScopeSelectionMode.All;
            return true;
        }

        scopeMode = default;
        return false;
    }
}