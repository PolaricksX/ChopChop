using System.Reflection;
using Cookbook.Api.Services;
using Cookbook.Data.Context;
using Cookbook.Domain.Entities;
using NetArchTest.Rules;

namespace Cookbook.UnitTests.Architecture;

public sealed class LayerRulesTests
{
    private static readonly Assembly Domain = typeof(Folder).Assembly;
    private static readonly Assembly Data = typeof(CookbookDbContext).Assembly;
    private static readonly Assembly Api = typeof(CookbookApplicationService).Assembly;

    [Fact]
    public void Domain_has_no_infrastructure_dependencies()
    {
        var result = Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny("Cookbook.Data", "Cookbook.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Data_has_no_api_or_aspnetcore_dependency()
    {
        var result = Types.InAssembly(Data).ShouldNot().HaveDependencyOnAny("Cookbook.Api", "Microsoft.AspNetCore").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Api_services_do_not_use_ef_core_directly()
    {
        var result = Types.InAssembly(Api).That().ResideInNamespaceStartingWith("Cookbook.Api.Services").Or().ResideInNamespaceStartingWith("Cookbook.Api.Contracts").ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
