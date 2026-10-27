namespace ReusableAccess.Api.IntegrationTests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Application_does_not_reference_infrastructure_or_ef_core()
    {
        var references = typeof(ReusableAccess.Application.DependencyInjection)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain(
            "ReusableAccess.Infrastructure",
            references);

        Assert.False(references.Any(name =>
            name!.StartsWith(
                "Microsoft.EntityFrameworkCore",
                StringComparison.Ordinal)));
    }
}