using System.Reflection;
namespace Pantry.Tests;

public sealed class PantryArchitectureTests
{
    [Fact]
    public void Domain_does_not_depend_on_framework_or_data_assemblies()
    {
        var references = typeof(Pantry.Domain.Entities.PantryItem).Assembly
            .GetReferencedAssemblies().Select(reference => reference.Name).ToHashSet();

        Assert.DoesNotContain("Pantry.Data", references);
        Assert.DoesNotContain("Microsoft.AspNetCore", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
    }

    [Fact]
    public void Data_depends_on_domain_but_domain_does_not_depend_on_data()
    {
        var dataReferences = typeof(Pantry.Data.Context.PantryDbContext).Assembly
            .GetReferencedAssemblies().Select(reference => reference.Name).ToHashSet();
        var domainReferences = typeof(Pantry.Domain.Entities.PantryItem).Assembly
            .GetReferencedAssemblies().Select(reference => reference.Name).ToHashSet();

        Assert.Contains("Pantry.Domain", dataReferences);
        Assert.DoesNotContain("Pantry.Data", domainReferences);
    }
}
