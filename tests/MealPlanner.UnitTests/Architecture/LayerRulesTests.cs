using System.Reflection;
using MealPlanner.Api.Services;
using MealPlanner.Data.Context;
using MealPlanner.Domain.Entities;
using NetArchTest.Rules;

namespace MealPlanner.UnitTests.Architecture;

public sealed class LayerRulesTests
{
    private static readonly Assembly Domain = typeof(MealPlan).Assembly;
    private static readonly Assembly Data = typeof(MealPlannerDbContext).Assembly;
    private static readonly Assembly Api = typeof(MealPlanService).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_data_efcore_or_aspnetcore()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("MealPlanner.Data", "MealPlanner.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "Forbidden domain dependencies: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Data_does_not_depend_on_api_or_aspnetcore()
    {
        var result = Types.InAssembly(Data)
            .ShouldNot()
            .HaveDependencyOnAny("MealPlanner.Api", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "Forbidden data dependencies: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Api_services_endpoints_and_contracts_do_not_depend_on_efcore_directly()
    {
        var result = Types.InAssembly(Api)
            .That()
            .ResideInNamespaceStartingWith("MealPlanner.Api.Services")
            .Or()
            .ResideInNamespaceStartingWith("MealPlanner.Api.Endpoints")
            .Or()
            .ResideInNamespaceStartingWith("MealPlanner.Api.Contracts")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "EF Core used directly from API layer: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
