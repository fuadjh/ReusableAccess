using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application.Completion;
using ReusableAccess.Application.Users;
using ReusableAccess.Contracts;

namespace ReusableAccess.Api.IntegrationTests;

public sealed class UserSliceBoundaryTests
{
    [Fact]
    public void User_handlers_depend_only_on_focused_store_contracts()
    {
        var readHandlers = new[]
        {
            typeof(SearchUsersHandler),
            typeof(GetCurrentUserHandler)
        };

        var writeHandlers = new[]
        {
            typeof(CreateManagedUserHandler),
            typeof(UpdateManagedUserHandler),
            typeof(SetManagedUserActiveHandler),
            typeof(ResetManagedPasswordHandler)
        };

        foreach (var handler in readHandlers)
        {
            var constructor = Assert.Single(handler.GetConstructors());
            var parameter = Assert.Single(constructor.GetParameters());
            Assert.Equal(typeof(IManagedUserReadStore), parameter.ParameterType);
        }

        foreach (var handler in writeHandlers)
        {
            var constructor = Assert.Single(handler.GetConstructors());
            var parameter = Assert.Single(constructor.GetParameters());
            Assert.Equal(typeof(IManagedUserCommandStore), parameter.ParameterType);
        }

        var removed = new[]
        {
            "SearchUsersAsync",
            "CreateUserAsync",
            "UpdateUserAsync",
            "SetActiveAsync",
            "ResetPasswordAsync",
            "CurrentUserAsync"
        };

        Assert.DoesNotContain(
            typeof(ICompletionStore).GetMethods(),
            method => removed.Contains(method.Name));
    }

    [Fact]
    public void Application_does_not_reference_infrastructure()
    {
        Assert.DoesNotContain(
            typeof(SearchUsers).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "ReusableAccess.Infrastructure");
    }

    [Fact]
    public async Task Each_user_request_has_exactly_one_registered_handler()
    {
        await using var host = await UserManagementTestHost.CreateAsync();

        await host.InScopeAsync(services =>
        {
            var pairs = new (Type Request, Type Response)[]
            {
                (typeof(SearchUsers), typeof(PageResult<UserRow>)),
                (typeof(GetCurrentUser), typeof(UserRow)),
                (typeof(CreateManagedUser), typeof(Guid)),
                (typeof(UpdateManagedUser), typeof(bool)),
                (typeof(SetManagedUserActive), typeof(bool)),
                (typeof(ResetManagedPassword), typeof(bool))
            };

            foreach (var pair in pairs)
            {
                var contract = typeof(IRequestHandler<,>)
                    .MakeGenericType(pair.Request, pair.Response);

                Assert.Single(services.GetServices(contract));
            }

            return Task.FromResult(true);
        });
    }
}