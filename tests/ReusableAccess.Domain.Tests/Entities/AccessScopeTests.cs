using System;
using ReusableAccess.Domain.Entities;
using Xunit;

namespace ReusableAccess.Domain.Tests.Entities;

public sealed class AccessScopeTests
{
    [Fact]
    public void Constructor_WhenValuesAreValid_CreatesScope()
    {
        var scope = new AccessScope(
            "clinic-01",
            "درمانگاه ۱");

        Assert.NotEqual(Guid.Empty, scope.Id);
        Assert.Equal("clinic-01", scope.Code);
        Assert.Equal("درمانگاه ۱", scope.DisplayName);
        Assert.Null(scope.ParentScopeId);
    }

    [Fact]
    public void Constructor_WhenParentIsProvided_StoresParentId()
    {
        var parentScopeId = Guid.NewGuid();

        var scope = new AccessScope(
            "clinic-01",
            "درمانگاه ۱",
            parentScopeId);

        Assert.Equal(parentScopeId, scope.ParentScopeId);
    }
}