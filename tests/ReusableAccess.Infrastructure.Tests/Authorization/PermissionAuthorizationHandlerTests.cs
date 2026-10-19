using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Authorization;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Tests.Authorization;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task Succeeds_when_user_has_active_assignment_and_permission()
    {
        var succeeded = await EvaluateAsync(
            roleCode: "CLINIC_ADMIN",
            scopeCode: "CLINIC_1",
            permissionKey: "workitems.read");

        Assert.True(succeeded);
    }

    [Fact]
    public async Task Denies_when_role_and_scope_are_not_assigned_to_user()
    {
        var succeeded = await EvaluateAsync(
            roleCode: "CLINIC_ADMIN",
            scopeCode: "HSE",
            permissionKey: "workitems.read");

        Assert.False(succeeded);
    }

    [Fact]
    public async Task Denies_when_assignment_is_disabled()
    {
        var succeeded = await EvaluateAsync(
            roleCode: "CLINIC_ADMIN",
            scopeCode: "CLINIC_1",
            permissionKey: "workitems.read",
            disableAssignment: true);

        Assert.False(succeeded);
    }

    [Fact]
    public async Task Denies_when_role_does_not_have_requested_permission()
    {
        var succeeded = await EvaluateAsync(
            roleCode: "CLINIC_ADMIN",
            scopeCode: "CLINIC_1",
            permissionKey: "users.delete");

        Assert.False(succeeded);
    }

    private static async Task<bool> EvaluateAsync(
        string roleCode,
        string scopeCode,
        string permissionKey,
        bool disableAssignment = false)
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var optionsBuilder =
      new DbContextOptionsBuilder<ApplicationDbContext>()
          .UseSqlite(connection);

        optionsBuilder.UseOpenIddict<Guid>();

        var options = optionsBuilder.Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "test-user",
            NormalizedUserName = "TEST-USER",
            NationalId = "TEST-001",
            DisplayName = "Test User"
        };

        var role = new Role("CLINIC_ADMIN", "Clinic administrator");
        var permission = new Permission(
            "workitems.read",
            "Read work items");

        role.AddPermission(permission);

        var clinicScope = new AccessScope(
            "CLINIC_1",
            "Clinic 1");

        var hseScope = new AccessScope(
            "HSE",
            "Health and safety");

        var assignment = new UserRoleAssignment(
            userId,
            role.Id,
            clinicScope.Id);

        if (disableAssignment)
        {
            assignment.Disable();
        }

        dbContext.AddRange(
            user,
            role,
            permission,
            clinicScope,
            hseScope,
            assignment);

        await dbContext.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Active-Role"] = roleCode;
        httpContext.Request.Headers["X-Active-Scope"] = scopeCode;

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim("sub", userId.ToString())],
                authenticationType: "Test"));

        var requirement = new PermissionRequirement(permissionKey);
        var authorizationContext = new AuthorizationHandlerContext(
            [requirement],
            principal,
            resource: null);

        var handler = new PermissionAuthorizationHandler(
            dbContext,
            httpContextAccessor);

        await handler.HandleAsync(authorizationContext);

        return authorizationContext.HasSucceeded;
    }
}