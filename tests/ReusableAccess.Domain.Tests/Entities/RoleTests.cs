using ReusableAccess.Domain.Entities;
using Xunit;

namespace ReusableAccess.Domain.Tests.Entities;

public sealed class RoleTests
{
    [Fact]
    public void AddPermission_WhenPermissionIsNew_AddsItToRole()
    {
        // Arrange
        var role = new Role("nurse", "پرستار");
        var permission = new Permission(
            "patients.read",
            "مشاهدهٔ پرونده‌ها");

        // Act
        var added = role.AddPermission(permission);

        // Assert
        Assert.True(added);
        Assert.Single(role.Permissions);
        Assert.Equal(permission.Id, role.Permissions.Single().PermissionId);
    }

    [Fact]
    public void AddPermission_WhenPermissionAlreadyExists_DoesNotAddDuplicate()
    {
        // Arrange
        var role = new Role("nurse", "پرستار");
        var permission = new Permission(
            "patients.read",
            "مشاهدهٔ پرونده‌ها");

        // Act
        var firstAdd = role.AddPermission(permission);
        var secondAdd = role.AddPermission(permission);

        // Assert
        Assert.True(firstAdd);
        Assert.False(secondAdd);
        Assert.Single(role.Permissions);
    }

    [Fact]
    public void AddPermission_WhenPermissionIsNull_ThrowsArgumentNullException()
    {
        var role = new Role("nurse", "پرستار");

        Assert.Throws<ArgumentNullException>(
            () => role.AddPermission(null!));
    }

    [Fact]
    public void RemovePermission_WhenPermissionExists_RemovesIt()
    {
        // Arrange
        var role = new Role("nurse", "پرستار");
        var permission = new Permission(
            "patients.read",
            "مشاهدهٔ پرونده‌ها");

        role.AddPermission(permission);

        // Act
        var removed = role.RemovePermission(permission.Id);

        // Assert
        Assert.True(removed);
        Assert.Empty(role.Permissions);
    }

    [Fact]
    public void RemovePermission_WhenPermissionDoesNotExist_ReturnsFalse()
    {
        var role = new Role("nurse", "پرستار");

        var removed = role.RemovePermission(Guid.NewGuid());

        Assert.False(removed);
    }
}