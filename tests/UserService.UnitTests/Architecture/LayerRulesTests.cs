using System.Reflection;
using NetArchTest.Rules;
using UserService.Api.Services;
using UserService.Data.Context;
using UserService.Domain.Entities;

namespace UserService.UnitTests.Architecture;

public class LayerRulesTests
{
    private static readonly Assembly Domain = typeof(UserProfile).Assembly;
    private static readonly Assembly Data = typeof(UserServiceDbContext).Assembly;
    private static readonly Assembly Api = typeof(UserProfileService).Assembly;

    private static void AssertNoDependencies(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        Assert.True(result.IsSuccessful, "Forbidden dependency in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_does_not_depend_on_data_efcore_or_aspnetcore() =>
        AssertNoDependencies(Domain, "UserService.Data", "UserService.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Data_does_not_depend_on_api_or_aspnetcore() =>
        AssertNoDependencies(Data, "UserService.Api", "Microsoft.AspNetCore");

    [Fact]
    public void Api_services_endpoints_and_contracts_do_not_use_efcore_directly()
    {
        var result = Types.InAssembly(Api)
            .That().ResideInNamespaceStartingWith("UserService.Api.Services")
            .Or().ResideInNamespaceStartingWith("UserService.Api.Endpoints")
            .Or().ResideInNamespaceStartingWith("UserService.Api.Contracts")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "EF Core used in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
