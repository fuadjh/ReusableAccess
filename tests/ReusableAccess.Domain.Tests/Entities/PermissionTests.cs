using ReusableAccess.Domain.Entities;
using Xunit;

namespace ReusableAccess.Domain.Tests.Entities;

public sealed class PermissionTests
{
    [Fact]
    public void Constructor_WhenValuesAreValid_CreatesPermission()
    {
        // Arrange
        const string key = "users.read";
        const string description = "مشاهده کاربران";

        // Act
        var permission = new Permission(key, description);

        // Assert
        Assert.NotEqual(Guid.Empty, permission.Id);
        Assert.Equal(key, permission.Key);
        Assert.Equal(description, permission.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenKeyIsMissing_ThrowsArgumentException(
        string? key)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Permission(key!, "مشاهده کاربران"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenDescriptionIsMissing_ThrowsArgumentException(
        string? description)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Permission("users.read", description!));
    }
}