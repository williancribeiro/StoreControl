using StoreControl.Application;
using StoreControl.Domain;

namespace StoreControl.UnitTests.Architecture;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void Domain_does_not_reference_other_StoreControl_projects()
    {
        var references = DomainAssembly.Reference
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith("StoreControl.", StringComparison.Ordinal) == true);

        Assert.Empty(references);
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure()
    {
        var references = ApplicationAssembly.Reference
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("StoreControl.Infrastructure", references);
    }
}
