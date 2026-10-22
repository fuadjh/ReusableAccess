using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.Endpoints;

public static class AccessProfilesEndpointExtensions
{
    public static IEndpointRouteBuilder MapAccessProfilesEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/me/access-profiles",
            async (
                HttpContext context,
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var subject = context.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(subject, out var userId))
                {
                    return Results.Forbid();
                }

                var user = await dbContext.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.Id == userId,
                        cancellationToken);

                if (user is null)
                {
                    return Results.Forbid();
                }

                var selectedProfiles = await (
                    from bundle in dbContext.Set<UserAccessBundle>()
                    join bundleRole in dbContext.Set<UserAccessBundleRole>()
                        on bundle.Id equals bundleRole.BundleId
                    join role in dbContext.Set<Role>()
                        on bundleRole.RoleId equals role.Id
                    join bundleScope in dbContext.Set<UserAccessBundleScope>()
                        on bundle.Id equals bundleScope.BundleId
                    join scope in dbContext.Set<AccessScope>()
                        on bundleScope.ScopeId equals scope.Id
                    where bundle.UserId == userId
                          && bundle.IsEnabled
                          && bundle.ScopeSelectionMode ==
                             ScopeSelectionMode.Selected
                    select new
                    {
                        bundleId = bundle.Id,
                        sortOrder = bundle.SortOrder,
                        bundleName = bundle.Name,
                        roleId = role.Id,
                        roleCode = role.Code,
                        scopeId = (Guid?)scope.Id,
                        scopeCode = (string?)scope.Code,
                        scopeMode = "selected"
                    })
                    .ToListAsync(cancellationToken);

                var allScopeProfiles = await (
                    from bundle in dbContext.Set<UserAccessBundle>()
                    join bundleRole in dbContext.Set<UserAccessBundleRole>()
                        on bundle.Id equals bundleRole.BundleId
                    join role in dbContext.Set<Role>()
                        on bundleRole.RoleId equals role.Id
                    where bundle.UserId == userId
                          && bundle.IsEnabled
                          && bundle.ScopeSelectionMode ==
                             ScopeSelectionMode.All
                    select new
                    {
                        bundleId = bundle.Id,
                        sortOrder = bundle.SortOrder,
                        bundleName = bundle.Name,
                        roleId = role.Id,
                        roleCode = role.Code,
                        scopeId = (Guid?)null,
                        scopeCode = (string?)null,
                        scopeMode = "all"
                    })
                    .ToListAsync(cancellationToken);

                var profiles = selectedProfiles
                    .Concat(allScopeProfiles)
                    .OrderBy(profile => profile.sortOrder)
                    .ThenBy(profile => profile.bundleName)
                    .ThenBy(profile => profile.roleCode)
                    .ThenBy(profile => profile.scopeMode)
                    .ThenBy(profile => profile.scopeCode)
                    .ToArray();

                var selectedProfile = profiles.FirstOrDefault(profile =>
                    profile.bundleId == user.LastSelectedAccessBundleId
                    && profile.roleId == user.LastSelectedAccessRoleId
                    && profile.scopeId == user.LastSelectedAccessScopeId);

                // ترجیح ذخیره‌شده اگر دیگر معتبر نباشد، به اولین گزینه برمی‌گردد.
                selectedProfile ??= profiles.FirstOrDefault();

                return Results.Ok(new
                {
                    data = profiles,
                    selectedProfile,
                    message = "بسته‌های دسترسی فعال کاربر."
                });
            })
            .RequireAuthorization("AccessApi");

        endpoints.MapPost(
            "/api/me/access-profile-preference",
            async (
                HttpContext context,
                ApplicationDbContext dbContext,
                AccessProfilePreferenceRequest request,
                CancellationToken cancellationToken) =>
            {
                var subject = context.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(subject, out var userId))
                {
                    return Results.Forbid();
                }

                var isSelectedMode = string.Equals(
                    request.ScopeMode,
                    "selected",
                    StringComparison.OrdinalIgnoreCase);

                var isAllMode = string.Equals(
                    request.ScopeMode,
                    "all",
                    StringComparison.OrdinalIgnoreCase);

                if (request.BundleId == Guid.Empty
                    || string.IsNullOrWhiteSpace(request.RoleCode)
                    || (!isSelectedMode && !isAllMode)
                    || (isSelectedMode
                        && string.IsNullOrWhiteSpace(request.ScopeCode))
                    || (isAllMode
                        && !string.IsNullOrWhiteSpace(request.ScopeCode)))
                {
                    return Results.BadRequest(
                        "مشخصات پروفایل انتخاب‌شده معتبر نیست.");
                }

                var user = await dbContext.Users
                    .SingleOrDefaultAsync(
                        item => item.Id == userId,
                        cancellationToken);

                if (user is null)
                {
                    return Results.Forbid();
                }

                var bundle = await dbContext.Set<UserAccessBundle>()
                    .SingleOrDefaultAsync(
                        item => item.Id == request.BundleId
                                && item.UserId == userId
                                && item.IsEnabled,
                        cancellationToken);

                var expectedMode = isSelectedMode
                    ? ScopeSelectionMode.Selected
                    : ScopeSelectionMode.All;

                if (bundle is null || bundle.ScopeSelectionMode != expectedMode)
                {
                    return Results.Forbid();
                }

                var roleId = await (
                    from bundleRole in dbContext.Set<UserAccessBundleRole>()
                    join role in dbContext.Set<Role>()
                        on bundleRole.RoleId equals role.Id
                    where bundleRole.BundleId == bundle.Id
                          && role.Code == request.RoleCode
                    select role.Id)
                    .SingleOrDefaultAsync(cancellationToken);

                if (roleId == Guid.Empty)
                {
                    return Results.Forbid();
                }

                Guid? scopeId = null;

                if (isSelectedMode)
                {
                    scopeId = await (
                        from bundleScope in dbContext.Set<UserAccessBundleScope>()
                        join scope in dbContext.Set<AccessScope>()
                            on bundleScope.ScopeId equals scope.Id
                        where bundleScope.BundleId == bundle.Id
                              && scope.Code == request.ScopeCode
                        select (Guid?)scope.Id)
                        .SingleOrDefaultAsync(cancellationToken);

                    if (scopeId is null)
                    {
                        return Results.Forbid();
                    }
                }

                user.LastSelectedAccessBundleId = bundle.Id;
                user.LastSelectedAccessRoleId = roleId;
                user.LastSelectedAccessScopeId = scopeId;

                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.Ok(new
                {
                    bundleId = bundle.Id,
                    roleId,
                    scopeId,
                    message = "ترجیح پروفایل ذخیره شد."
                });
            })
            .RequireAuthorization("AccessApi");

        return endpoints;
    }
}

public sealed record AccessProfilePreferenceRequest(
    Guid BundleId,
    string RoleCode,
    string? ScopeCode,
    string ScopeMode);