using System.Reflection;
using Equipment.Api.Services;
using Equipment.Data.Context;
using Equipment.Domain.Entities;
using NetArchTest.Rules;

namespace Equipment.UnitTests.Architecture;

public sealed class LayerRulesTests
{
    private static readonly Assembly Domain = typeof(EquipmentItem).Assembly;
    private static readonly Assembly Data = typeof(EquipmentDbContext).Assembly;
    private static readonly Assembly Api = typeof(EquipmentApplicationService).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_data_api_efcore_or_aspnetcore()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Equipment.Data",
                "Equipment.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "Forbidden dependency in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Data_does_not_depend_on_api_or_aspnetcore()
    {
        var result = Types.InAssembly(Data)
            .ShouldNot()
            .HaveDependencyOnAny("Equipment.Api", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "Forbidden dependency in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Api_services_endpoints_and_contracts_do_not_use_efcore_directly()
    {
        var result = Types.InAssembly(Api)
            .That().ResideInNamespaceStartingWith("Equipment.Api.Services")
            .Or().ResideInNamespaceStartingWith("Equipment.Api.Endpoints")
            .Or().ResideInNamespaceStartingWith("Equipment.Api.Contracts")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "EF Core used in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
