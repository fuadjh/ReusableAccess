using ReusableAccess.Domain.Entities;
using Xunit;
using Assert = Xunit.Assert;

namespace ReusableAccess.Domain.Tests;

public sealed class RoleValidationTests
{
    [Fact]
    public void Create_trims_values_without_changing_code_case()
    {
        var role = new Role(" Admin ", " Administrator ");

        Assert.NotEqual(Guid.Empty, role.Id);
        Assert.Equal("Admin", role.Code);
        Assert.Equal("Administrator", role.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_code(string? code)
    {
        // null! برای آزمایش ورودی نامعتبر در زمان اجراست.
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            _ = new Role(code!, "Administrator");
        });
    }



    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_and_rename_reject_missing_display_name(
        string? displayName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            _ = new Role("admin", displayName!);
        });

        var role = new Role("admin", "Old name");

        Assert.ThrowsAny<ArgumentException>(() =>
        {
            role.Rename(displayName!);
        });

        Assert.Equal("Old name", role.DisplayName);
    }

    [Fact]
    public void Create_accepts_maximum_lengths_after_trimming()
    {
        var code = new string('c', Role.CodeMaxLength);
        var displayName = new string('n', Role.DisplayNameMaxLength);

        var role = new Role(
            $" {code} ",
            $" {displayName} ");

        Assert.Equal(code, role.Code);
        Assert.Equal(displayName, role.DisplayName);
    }

    [Fact]
    public void Create_rejects_code_above_maximum_length()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new Role(
                new string('c', Role.CodeMaxLength + 1),
                "Administrator");
        });

        Assert.Equal("code", exception.ParamName);
    }

    [Fact]
    public void Create_and_rename_reject_the_same_overlong_display_name()
    {
        var displayName =
            new string('n', Role.DisplayNameMaxLength + 1);

        var createException =
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                _ = new Role("admin", displayName);
            });

        var role = new Role("admin", "Old name");

        var renameException =
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                role.Rename(displayName);
            });

        Assert.Equal("displayName", createException.ParamName);
        Assert.Equal("displayName", renameException.ParamName);

        // شکست رفتار نباید وضعیت موجودیت را نیمه‌کاره تغییر دهد.
        Assert.Equal("Old name", role.DisplayName);
        Assert.Equal("admin", role.Code);
    }
    [Fact]
    public void Length_limits_match_the_existing_contract()
    {
        Assert.Equal(100, Role.CodeMaxLength);
        Assert.Equal(200, Role.DisplayNameMaxLength);
    }
    [Fact]
    public void Rename_accepts_maximum_length_after_trimming()
    {
        var role = new Role("admin", "Old name");
        var originalId = role.Id;
        var displayName = new string('n', Role.DisplayNameMaxLength);

        role.Rename($" {displayName} ");

        Assert.Equal(displayName, role.DisplayName);
        Assert.Equal(originalId, role.Id);
        Assert.Equal("admin", role.Code);
    }
}