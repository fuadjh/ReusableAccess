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
                    orderby bundle.Name, role.Code, scope.Code
                    select new
                    {
                        bundleId = bundle.Id,
                        bundleName = bundle.Name,
                        roleCode = role.Code,
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
                    orderby bundle.Name, role.Code
                    select new
                    {
                        bundleId = bundle.Id,
                        bundleName = bundle.Name,
                        roleCode = role.Code,
                        scopeCode = (string?)null,
                        scopeMode = "all"
                    })
                    .ToListAsync(cancellationToken);

                var profiles = selectedProfiles
                    .Concat(allScopeProfiles)
                    .OrderBy(profile => profile.bundleName)
                    .ThenBy(profile => profile.roleCode)
                    .ThenBy(profile => profile.scopeMode)
                    .ThenBy(profile => profile.scopeCode)
                    .ToArray();

                return Results.Ok(new
                {
                    data = profiles,
                    message = "بسته‌های دسترسی فعال کاربر."
                });
            })
            .RequireAuthorization("AccessApi");

        return endpoints;
    }
}