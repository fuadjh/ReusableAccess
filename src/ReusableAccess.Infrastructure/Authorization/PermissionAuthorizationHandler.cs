using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        var httpContext = httpContextAccessor.HttpContext;

        var roleCode = httpContext?.Request.Headers["X-Active-Role"].ToString();
        var scopeCode = httpContext?.Request.Headers["X-Active-Scope"].ToString();

        if (!Guid.TryParse(subject, out var userId) ||
            string.IsNullOrWhiteSpace(roleCode) ||
            string.IsNullOrWhiteSpace(scopeCode))
        {
            return;
        }

        var hasPermission =
            await (from assignment in dbContext.Set<UserRoleAssignment>()
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
                         && permission.Key == requirement.PermissionKey
                   select permission.Id)
                .AnyAsync(httpContext!.RequestAborted);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}