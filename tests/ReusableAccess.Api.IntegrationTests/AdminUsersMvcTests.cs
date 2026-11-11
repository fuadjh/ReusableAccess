using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Api.Controllers;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.IntegrationTests;

[Trait("Feature", "UserManagement")]
public sealed class AdminUsersMvcTests
{
    private const string Root = "/api/admin/users";
    private const string NationalId = "0012345679";
    private const string Password = "Test-Only.Strong42!";

    [Fact]
    public void Controller_is_thin_and_requires_platform_admin()
    {
        var type = typeof(AdminUsersController);
        var constructor = Assert.Single(type.GetConstructors());
        Assert.Equal(typeof(ISender), Assert.Single(constructor.GetParameters()).ParameterType);
        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.Equal("PlatformAdmin", Assert.Single(type.GetCustomAttributes<AuthorizeAttribute>()).Policy);

        // هر action باید لغو درخواست را به MediatR برساند؛ وابستگی به EF یا Identity مجاز نیست.
        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(6, actions.Length);
        Assert.All(actions, action => Assert.Contains(
            action.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken)));
    }

    [Fact]
    public async Task Each_management_route_is_one_MVC_action_not_a_duplicate_minimal_endpoint()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        var endpoints = ((IEndpointRouteBuilder)host.App).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();

        var routes = new (string Method, string Path)[]
        {
            ("GET", "api/admin/users/manage"),
            ("GET", "api/admin/users/{id:guid}/details"),
            ("POST", "api/admin/users"),
            ("PUT", "api/admin/users/{id:guid}"),
            ("PUT", "api/admin/users/{id:guid}/active"),
            ("POST", "api/admin/users/{id:guid}/reset-password")
        };

        foreach (var (method, path) in routes)
        {
            var endpoint = Assert.Single(endpoints.Where(endpoint =>
                string.Equals(endpoint.RoutePattern.RawText?.TrimStart('/'), path,
                    StringComparison.OrdinalIgnoreCase) &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true));
            var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            Assert.NotNull(action);
            Assert.Equal(typeof(AdminUsersController), action.ControllerTypeInfo.AsType());
        }
    }

    [Theory]
    [InlineData("GET", "/manage")]
    [InlineData("GET", "/{id}/details")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/{id}")]
    [InlineData("PUT", "/{id}/active")]
    [InlineData("POST", "/{id}/reset-password")]
    public async Task Every_route_returns_401_for_anonymous_and_403_for_regular_users(
        string method, string suffix)
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        using var anonymousRequest = ManagementRequest(method, suffix, host.ReaderId);
        using var anonymous = await host.Client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        host.As(host.ReaderId);
        using var regularRequest = ManagementRequest(method, suffix, host.ReaderId);
        using var regular = await host.Client.SendAsync(regularRequest);
        Assert.Equal(HttpStatusCode.Forbidden, regular.StatusCode);
        Assert.Equal(2, await host.InScopeAsync(services =>
            services.GetRequiredService<ApplicationDbContext>().Users.CountAsync()));
    }

    [Fact]
    public async Task Listing_preserves_data_envelope_paging_and_filtering()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        using var all = await host.Client.GetAsync($"{Root}/manage");
        var first = await DataAsync<PageResult<UserRow>>(all);
        Assert.Equal(1, first.Page);
        Assert.Equal(20, first.PageSize);
        Assert.Equal(2, first.Total);
        Assert.Equal(2, first.Items.Count);

        using var filtered = await host.Client.GetAsync($"{Root}/manage?q=Reader&page=0");
        var page = await DataAsync<PageResult<UserRow>>(filtered);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.Total);
        Assert.Equal(host.ReaderId, Assert.Single(page.Items).UserId);
    }

    [Fact]
    public async Task Creation_preserves_200_and_uses_normalized_national_id_as_username()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        var input = NewInput();
        // فیلدهای خارج از DTO نباید امکان ارتقای مجوز یا جعل ActorId بدهند.
        using var response = await host.Client.PostAsJsonAsync(Root, new
        {
            input.FirstName,
            input.LastName,
            input.OrganizationalCode,
            NationalId = " ۰۰۱۲۳۴۵۶۷۹ ",
            input.Email,
            Mobile = "۰۹۱۲۳۴۵۶۷۸۹",
            input.Password,
            UserName = "must-not-be-used",
            IsPlatformSuperAdmin = true,
            IsActive = false,
            SessionVersion = 999,
            ActorId = host.ReaderId
        });
        var id = await DataAsync<Guid>(response);
        Assert.NotEqual(Guid.Empty, id);

        var row = await DetailsAsync(host, id);
        Assert.Equal(NationalId, row.NationalId);
        Assert.Equal(NationalId, row.UserName);
        Assert.Equal("Alice", row.FirstName);
        Assert.Equal("Tester", row.LastName);
        Assert.Equal("Alice Tester", row.DisplayName);
        Assert.Equal("ORG-100", row.OrganizationalCode);
        Assert.Equal("+989123456789", row.Mobile);
        Assert.True(row.IsActive);
        Assert.False(row.IsPlatformSuperAdmin);
        Assert.False(string.IsNullOrWhiteSpace(row.ConcurrencyStamp));

        var state = await host.InScopeAsync(async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(user => user.Id == id);
            return (user.MustChangePassword, user.SessionVersion);
        });
        Assert.True(state.MustChangePassword);
        Assert.Equal(0, state.SessionVersion);
    }

    [Fact]
    public async Task Details_and_list_do_not_disclose_password_hash_or_security_stamp()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        var id = await CreateAsync(host);
        using var details = await host.Client.GetAsync($"{Root}/{id}/details");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using var detailJson = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
        AssertNoSecrets(detailJson.RootElement.GetProperty("data"));

        using var list = await host.Client.GetAsync($"{Root}/manage?q={NationalId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var rows = listJson.RootElement.GetProperty("data").GetProperty("items");
        Assert.Single(rows.EnumerateArray());
        foreach (var row in rows.EnumerateArray())
            AssertNoSecrets(row);
    }

    [Fact]
    public async Task Duplicate_national_id_is_409_even_when_digits_use_another_alphabet()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        await CreateAsync(host);
        var duplicate = NewInput();
        duplicate.NationalId = "۰۰۱۲۳۴۵۶۷۹";
        duplicate.Email = "different@example.test";
        using var response = await host.Client.PostAsJsonAsync(Root, duplicate);
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(1, await host.InScopeAsync(services =>
            services.GetRequiredService<ApplicationDbContext>().Users.CountAsync(
                user => user.NationalId == NationalId)));
    }

    [Fact]
    public async Task Editing_uses_concurrency_stamp_and_stale_edit_cannot_overwrite()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        var id = await CreateAsync(host);
        var original = await DetailsAsync(host, id);

        using var saved = await host.Client.PutAsJsonAsync($"{Root}/{id}", UpdateInput(original, "Changed"));
        Assert.True(await DataAsync<bool>(saved));
        var current = await DetailsAsync(host, id);
        Assert.NotEqual(original.ConcurrencyStamp, current.ConcurrencyStamp);

        using var stale = await host.Client.PutAsJsonAsync($"{Root}/{id}", UpdateInput(original, "Lost update"));
        await AssertProblemAsync(stale, HttpStatusCode.Conflict);
        Assert.Equal("Changed", (await DetailsAsync(host, id)).FirstName);
    }

    [Fact]
    public async Task A_valid_national_id_is_immutable_after_creation()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        var id = await CreateAsync(host);
        var change = UpdateInput(await DetailsAsync(host, id));
        change.NationalId = "0023456787";
        using var response = await host.Client.PutAsJsonAsync($"{Root}/{id}", change);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(NationalId, (await DetailsAsync(host, id)).NationalId);
    }

    [Fact]
    public async Task Missing_user_is_404_with_problem_details()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        using var response = await host.Client.GetAsync($"{Root}/{Guid.NewGuid()}/details");
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Client_supplied_actor_id_cannot_bypass_self_protection()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        var self = await DetailsAsync(host, host.AdminId);
        using var deactivate = await host.Client.PutAsJsonAsync($"{Root}/{self.UserId}/active", new
        {
            IsActive = false,
            self.ConcurrencyStamp,
            ActorId = host.ReaderId
        });
        await AssertProblemAsync(deactivate, HttpStatusCode.Conflict);

        using var reset = await host.Client.PostAsJsonAsync($"{Root}/{self.UserId}/reset-password", new
        {
            Password,
            ActorId = host.ReaderId
        });
        await AssertProblemAsync(reset, HttpStatusCode.BadRequest);
        Assert.True((await DetailsAsync(host, host.AdminId)).IsActive);
    }

    [Fact]
    public async Task Deactivation_invalidates_old_session_and_rejects_stale_status_change()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        var id = await CreateAsync(host);
        var row = await DetailsAsync(host, id);

        host.As(id);
        using var before = await host.Client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        host.As(host.AdminId);
        using var disable = await host.Client.PutAsJsonAsync($"{Root}/{id}/active",
            new ActiveUserInput(false, row.ConcurrencyStamp));
        Assert.True(await DataAsync<bool>(disable));
        using var stale = await host.Client.PutAsJsonAsync($"{Root}/{id}/active",
            new ActiveUserInput(true, row.ConcurrencyStamp));
        await AssertProblemAsync(stale, HttpStatusCode.Conflict);
        Assert.False((await DetailsAsync(host, id)).IsActive);

        host.As(id);
        using var after = await host.Client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Password_reset_changes_password_requires_change_and_invalidates_old_session()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        var id = await CreateAsync(host);
        // وضعیت کاربری که قبلاً گذرواژهٔ موقت را عوض کرده است، به‌عنوان دادهٔ تست آماده می‌شود.
        await host.InScopeAsync(async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(user => user.Id == id);
            user.MustChangePassword = false;
            return await db.SaveChangesAsync();
        });

        host.As(id);
        using var before = await host.Client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        host.As(host.AdminId);
        const string replacement = "Reset-Only.Strong84!";
        using var reset = await host.Client.PostAsJsonAsync($"{Root}/{id}/reset-password",
            new AdminPasswordInput(replacement));
        Assert.True(await DataAsync<bool>(reset));
        var state = await host.InScopeAsync(async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var manager = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var user = await db.Users.SingleAsync(user => user.Id == id);
            return (user.MustChangePassword, user.SessionVersion,
                NewWorks: await manager.CheckPasswordAsync(user, replacement),
                OldWorks: await manager.CheckPasswordAsync(user, Password));
        });
        Assert.True(state.MustChangePassword);
        Assert.Equal(1, state.SessionVersion);
        Assert.True(state.NewWorks);
        Assert.False(state.OldWorks);

        host.As(id, sessionVersion: 0);
        using var after = await host.Client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Separate_consumer_databases_have_independent_user_populations()
    {
        await using var first = await UserManagementTestHost.CreateAsync();
        await using var second = await UserManagementTestHost.CreateAsync();
        first.As(first.AdminId);
        second.As(second.AdminId);
        var firstId = await CreateAsync(first);
        using var absent = await second.Client.GetAsync($"{Root}/{firstId}/details");
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);

        // همان کد ملی و ایمیل در دیتابیس مصرف‌کنندهٔ دوم، تعارض ایجاد نمی‌کند.
        var secondId = await CreateAsync(second);
        Assert.NotEqual(firstId, secondId);
        using var reverse = await first.Client.GetAsync($"{Root}/{secondId}/details");
        Assert.Equal(HttpStatusCode.NotFound, reverse.StatusCode);
        Assert.Equal(NationalId, (await DetailsAsync(first, firstId)).UserName);
        Assert.Equal(NationalId, (await DetailsAsync(second, secondId)).UserName);
    }

    [Fact]
    public async Task Invalid_http_model_returns_400_validation_problem_details()
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        host.As(host.AdminId);
        var input = NewInput();
        input.Password = "short";
        using var response = await host.Client.PostAsJsonAsync(Root, input);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("errors").ValueKind);
    }

    [Theory]
    [InlineData("FirstName")]
    [InlineData("LastName")]
    [InlineData("OrganizationalCode")]
    [InlineData("NationalId")]
    [InlineData("Email")]
    [InlineData("Mobile")]
    [InlineData("Password")]
    public async Task Direct_mediator_calls_cannot_skip_profile_validation(string field)
    {
        await using var host = await UserManagementTestHost.CreateAsync();
        var input = NewInput();
        typeof(CreateUserInput).GetProperty(field)!.SetValue(input, field switch
        {
            "NationalId" => "0000000000",
            "Email" => "invalid-email",
            "Mobile" => "invalid-mobile",
            "Password" => "short",
            _ => " "
        });

        await host.InScopeAsync(async services =>
        {
            var sender = services.GetRequiredService<ISender>();
            var createError = await Assert.ThrowsAsync<CompletionException>(() =>
                sender.Send(new CreateManagedUser(host.AdminId, input)));
            Assert.Equal(400, createError.StatusCode);

            if (field != "Password")
            {
                var update = new UpdateUserInput
                {
                    FirstName = input.FirstName,
                    LastName = input.LastName,
                    OrganizationalCode = input.OrganizationalCode,
                    NationalId = input.NationalId,
                    Email = input.Email,
                    Mobile = input.Mobile
                };
                var updateError = await Assert.ThrowsAsync<CompletionException>(() =>
                    sender.Send(new UpdateManagedUser(host.AdminId, host.ReaderId, update)));
                Assert.Equal(400, updateError.StatusCode);
            }
            return true;
        });
        Assert.Equal(2, await host.InScopeAsync(services =>
            services.GetRequiredService<ApplicationDbContext>().Users.CountAsync()));
    }

    private static CreateUserInput NewInput() => new()
    {
        FirstName = "  Alice  ",
        LastName = "  Tester  ",
        OrganizationalCode = "  ORG-100  ",
        NationalId = NationalId,
        Email = "alice@example.test",
        Mobile = "09123456789",
        Password = Password
    };

    private static UpdateUserInput UpdateInput(UserRow row, string? firstName = null) => new()
    {
        FirstName = firstName ?? row.FirstName,
        LastName = row.LastName,
        OrganizationalCode = row.OrganizationalCode,
        NationalId = row.NationalId,
        Email = row.Email,
        Mobile = row.Mobile,
        ConcurrencyStamp = row.ConcurrencyStamp
    };

    private static HttpRequestMessage ManagementRequest(string method, string suffix, Guid id)
    {
        var request = new HttpRequestMessage(new HttpMethod(method),
            Root + suffix.Replace("{id}", id.ToString()));
        if (method != "GET")
        {
            var input = NewInput();
            request.Content = JsonContent.Create(new
            {
                input.FirstName,
                input.LastName,
                input.OrganizationalCode,
                input.NationalId,
                input.Email,
                input.Mobile,
                input.Password,
                ConcurrencyStamp = "test-stamp",
                IsActive = false
            });
        }
        return request;
    }

    private static async Task<Guid> CreateAsync(UserManagementTestHost host)
    {
        using var response = await host.Client.PostAsJsonAsync(Root, NewInput());
        return await DataAsync<Guid>(response);
    }

    private static async Task<UserRow> DetailsAsync(UserManagementTestHost host, Guid id)
    {
        using var response = await host.Client.GetAsync($"{Root}/{id}/details");
        return await DataAsync<UserRow>(response);
    }

    private static async Task<T> DataAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DataEnvelope<T>>();
        Assert.NotNull(result);
        return result.Data;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
    }

    private static void AssertNoSecrets(JsonElement row)
    {
        var names = row.EnumerateObject().Select(property => property.Name).ToArray();
        foreach (var secret in new[] { "password", "passwordHash", "securityStamp", "sessionVersion" })
            Assert.DoesNotContain(names, name => string.Equals(name, secret, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record DataEnvelope<T>(T Data);
}