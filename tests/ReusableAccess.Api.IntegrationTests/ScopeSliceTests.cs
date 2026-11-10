using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class ScopeSliceTests
{
    [Fact]
    public async Task Crud_preserves_contract_and_immutable_fields()
    {
        await using var host =
            await RoleAndScopeTestHost.CreateAsync();

        var parentId = await CreateAsync(
            host.Client, "PARENT");

        var scopeId = await CreateAsync(
            host.Client, " CHILD ", parentId);

        // فیلدهای اضافی نباید بتوانند Code یا ParentScopeId را تغییر دهند.
        using var update = await host.Client.PutAsJsonAsync(
            $"/api/admin/scopes/{scopeId}",
            new
            {
                displayName = " Updated ",
                code = "SHOULD_NOT_CHANGE",
                parentScopeId = (Guid?)null
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using (var json = JsonDocument.Parse(
                   await update.Content.ReadAsStringAsync()))
        {
            var data = json.RootElement.GetProperty("data");

            Assert.Equal(
                "CHILD",
                data.GetProperty("scopeCode").GetString());

            Assert.Equal(
                "Updated",
                data.GetProperty("displayName").GetString());

            Assert.Equal(
                parentId,
                data.GetProperty("parentScopeId").GetGuid());
        }

        using var list = await host.Client.GetAsync(
            "/api/admin/scopes");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using (var json = JsonDocument.Parse(
                   await list.Content.ReadAsStringAsync()))
        {
            var codes = json.RootElement.GetProperty("data")
                .EnumerateArray()
                .Select(item =>
                    item.GetProperty("scopeCode").GetString())
                .ToArray();

            Assert.Equal(
                new[] { "CHILD", "PARENT" },
                codes);
        }

        using var deleted = await host.Client.DeleteAsync(
            $"/api/admin/scopes/{scopeId}");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        using (var json = JsonDocument.Parse(
                   await deleted.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("data").GetBoolean());
        }

        using var missing = await host.Client.DeleteAsync(
            $"/api/admin/scopes/{scopeId}");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        await host.InScopeAsync(async services =>
        {
            var db = services
                .GetRequiredService<ApplicationDbContext>();

            Assert.False(
                await db.Set<AccessScope>().AnyAsync(
                    item => item.Id == scopeId));

            Assert.True(
                await db.Set<AccessScope>().AnyAsync(
                    item => item.Id == parentId));
        });
    }

    [Fact]
    public async Task Creation_checks_duplicate_code_and_parent()
    {
        await using var host =
            await RoleAndScopeTestHost.CreateAsync();

        await CreateAsync(host.Client, "DUP");

        using var duplicate = await host.Client.PostAsJsonAsync(
            "/api/admin/scopes",
            new
            {
                code = " DUP ",
                displayName = "Duplicate"
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicate.StatusCode);

        using var missingParent = await host.Client.PostAsJsonAsync(
            "/api/admin/scopes",
            new
            {
                code = "OTHER",
                displayName = "Other",
                parentScopeId = Guid.NewGuid()
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            missingParent.StatusCode);
    }

    [Fact]
    public async Task Direct_mediator_calls_cannot_skip_input_validation()
    {
        await using var host =
            await RoleAndScopeTestHost.CreateAsync();

        await host.InScopeAsync(async services =>
        {
            var sender = services.GetRequiredService<ISender>();

            await Assert.ThrowsAsync<ScopeInputException>(() =>
                sender.Send(
                    new CreateScopeCommand(" ", "Name", null)));

            await Assert.ThrowsAsync<ScopeInputException>(() =>
                sender.Send(
                    new CreateScopeCommand(
                        "VALID", "Name", Guid.Empty)));

            await Assert.ThrowsAsync<ScopeInputException>(() =>
                sender.Send(
                    new RenameScopeCommand(
                        Guid.NewGuid(), " ")));

            var db = services
                .GetRequiredService<ApplicationDbContext>();

            Assert.False(
                await db.Set<AccessScope>().AnyAsync());
        });
    }

    [Theory]
    [InlineData("child")]
    [InlineData("workitem")]
    [InlineData("assignment")]
    [InlineData("bundle")]
    public async Task Referenced_scope_is_not_deleted(
        string dependency)
    {
        await using var host =
            await RoleAndScopeTestHost.CreateAsync();

        var scopeId = await CreateAsync(
            host.Client, "IN_USE");

        await host.InScopeAsync(async services =>
        {
            var db = services
                .GetRequiredService<ApplicationDbContext>();

            if (dependency == "child")
            {
                db.Add(new AccessScope(
                    "DEPENDENT_CHILD",
                    "Child",
                    scopeId));
            }
            else if (dependency == "workitem")
            {
                db.Add(new WorkItem(
                    scopeId,
                    "Test item",
                    "Open"));
            }
            else
            {
                var userId = Guid.NewGuid();

                var user = new ApplicationUser
                {
                    Id = userId,
                    UserName = "scope-test-user",
                    NationalId = $"SCOPE-{userId:N}",
                    DisplayName = "Scope test user"
                };

                var role = new Role(
                    "SCOPE_TEST_ROLE",
                    "Scope test role");

                db.AddRange(user, role);
                await db.SaveChangesAsync();

                if (dependency == "assignment")
                {
                    db.Add(new UserRoleAssignment(
                        user.Id,
                        role.Id,
                        scopeId));
                }
                else
                {
                    db.Add(new UserAccessBundle(
                        user.Id,
                        "Scope test bundle",
                        [role.Id],
                        ScopeSelectionMode.Selected,
                        [scopeId]));
                }
            }

            await db.SaveChangesAsync();
        });

        using var response = await host.Client.DeleteAsync(
            $"/api/admin/scopes/{scopeId}");

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using (var json = JsonDocument.Parse(
                   await response.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "محدوده دارای فرزند، تخصیص یا داده است.",
                json.RootElement.GetProperty("detail").GetString());
        }

        await host.InScopeAsync(async services =>
        {
            var db = services
                .GetRequiredService<ApplicationDbContext>();

            Assert.True(
                await db.Set<AccessScope>().AnyAsync(
                    item => item.Id == scopeId));
        });
    }

    [Fact]
    public void Scope_controller_depends_only_on_sender()
    {
        var controllerType =
            typeof(ReusableAccess.Api.Controllers.AdminScopesController);

        var constructor =
            Assert.Single(controllerType.GetConstructors());

        var parameter =
            Assert.Single(constructor.GetParameters());

        Assert.Equal(
            typeof(ISender),
            parameter.ParameterType);
    }

    private static async Task<Guid> CreateAsync(
        HttpClient client,
        string code,
        Guid? parentScopeId = null)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/admin/scopes",
            new
            {
                code,
                displayName = $" {code.Trim()} ",
                parentScopeId
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var data = json.RootElement.GetProperty("data");

        Assert.Equal(
            code.Trim(),
            data.GetProperty("scopeCode").GetString());

        return data.GetProperty("scopeId").GetGuid();
    }
}