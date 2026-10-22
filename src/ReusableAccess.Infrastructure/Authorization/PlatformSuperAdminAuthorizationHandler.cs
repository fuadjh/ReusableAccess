using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Authorization;

public sealed class PlatformSuperAdminAuthorizationHandler(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PlatformSuperAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformSuperAdminRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null || !Guid.TryParse(subject, out var userId))
        {
            return;
        }

        var isPlatformSuperAdmin = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == userId && user.IsPlatformSuperAdmin,
                httpContext.RequestAborted);

        if (isPlatformSuperAdmin)
        {
            context.Succeed(requirement);
        }
    }
}