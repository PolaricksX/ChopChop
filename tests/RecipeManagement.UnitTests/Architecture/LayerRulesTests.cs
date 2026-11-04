using System.Reflection;
using NetArchTest.Rules;
using RecipeManagement.Api.Services;
using RecipeManagement.Data.Context;
using RecipeManagement.Domain.Entities;

namespace RecipeManagement.UnitTests.Architecture;

public sealed class LayerRulesTests
{
    private static readonly Assembly Domain = typeof(Recipe).Assembly;
    private static readonly Assembly Data = typeof(RecipeManagementDbContext).Assembly;
    private static readonly Assembly Api = typeof(RecipeApplicationService).Assembly;

    [Fact]
    public void Domain_has_no_infrastructure_dependencies()
    {
        var result = Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny(
            "RecipeManagement.Data", "RecipeManagement.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore").GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Data_has_no_api_or_aspnetcore_dependency()
    {
        var result = Types.InAssembly(Data).ShouldNot().HaveDependencyOnAny(
            "RecipeManagement.Api", "Microsoft.AspNetCore").GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Api_services_do_not_use_ef_core_directly()
    {
        var result = Types.InAssembly(Api).That().ResideInNamespaceStartingWith("RecipeManagement.Api.Services")
            .Or().ResideInNamespaceStartingWith("RecipeManagement.Api.Contracts")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore").GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
