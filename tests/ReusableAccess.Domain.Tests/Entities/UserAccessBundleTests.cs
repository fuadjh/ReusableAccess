using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;

namespace ReusableAccess.Domain.Tests.Entities;

public sealed class UserAccessBundleTests
{
    [Fact]
    public void SelectedScopes_AllowsOnlyRolesAndScopesInThisBundle()
    {
        var roleId = Guid.NewGuid();
        var anotherRoleId = Guid.NewGuid();
        var clinic1Id = Guid.NewGuid();
        var clinic2Id = Guid.NewGuid();
        var clinic3Id = Guid.NewGuid();

        var bundle = new UserAccessBundle(
            Guid.NewGuid(),
            "پرستار درمانگاه‌ها",
            [roleId],
            ScopeSelectionMode.Selected,
            [clinic1Id, clinic2Id]);

        Assert.True(bundle.Allows(roleId, clinic1Id));
        Assert.True(bundle.Allows(roleId, clinic2Id));
        Assert.False(bundle.Allows(roleId, clinic3Id));
        Assert.False(bundle.Allows(anotherRoleId, clinic1Id));
    }

    [Fact]
    public void AllScopes_AllowsTheRoleForAFutureScope()
    {
        var roleId = Guid.NewGuid();
        var futureScopeId = Guid.NewGuid();

        var bundle = new UserAccessBundle(
            Guid.NewGuid(),
            "خدمت‌گیرنده",
            [roleId],
            ScopeSelectionMode.All);

        Assert.True(bundle.Allows(roleId, futureScopeId));
    }

    [Fact]
    public void SelectedScopes_RequiresAtLeastOneScope()
    {
        Assert.Throws<ArgumentException>(() =>
            new UserAccessBundle(
                Guid.NewGuid(),
                "بستهٔ ناقص",
                [Guid.NewGuid()],
                ScopeSelectionMode.Selected));
    }

    [Fact]
    public void AllScopes_CannotAlsoContainExplicitScopes()
    {
        Assert.Throws<ArgumentException>(() =>
            new UserAccessBundle(
                Guid.NewGuid(),
                "بستهٔ همه‌اسکوپی",
                [Guid.NewGuid()],
                ScopeSelectionMode.All,
                [Guid.NewGuid()]));
    }

    [Fact]
    public void Bundle_RequiresAtLeastOneRole()
    {
        Assert.Throws<ArgumentException>(() =>
            new UserAccessBundle(
                Guid.NewGuid(),
                "بستهٔ بدون نقش",
                [],
                ScopeSelectionMode.All));
    }
}