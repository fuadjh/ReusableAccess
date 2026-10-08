using System;
using ReusableAccess.Domain.Entities;
using Xunit;

namespace ReusableAccess.Domain.Tests.Entities;

public sealed class UserRoleAssignmentTests
{
    [Fact]
    public void Constructor_WhenIdsAreValid_CreatesEnabledAssignment()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();

        var assignment = new UserRoleAssignment(
            userId,
            roleId,
            scopeId);

        Assert.NotEqual(Guid.Empty, assignment.Id);
        Assert.Equal(userId, assignment.UserId);
        Assert.Equal(roleId, assignment.RoleId);
        Assert.Equal(scopeId, assignment.ScopeId);
        Assert.True(assignment.IsEnabled);
    }

    [Fact]
    public void Constructor_WhenUserIdIsEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new UserRoleAssignment(
                Guid.Empty,
                Guid.NewGuid(),
                Guid.NewGuid()));
    }

    [Fact]
    public void DisableAndEnable_ChangeAssignmentState()
    {
        var assignment = new UserRoleAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        assignment.Disable();

        Assert.False(assignment.IsEnabled);

        assignment.Enable();

        Assert.True(assignment.IsEnabled);
    }
}