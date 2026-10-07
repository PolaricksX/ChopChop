using NetArchTest.Rules;
using Xunit;

namespace UserPantry.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Dependencies_PointInward()
    {
        var domain = typeof(UserPantry.Domain.PantryItem).Assembly;
        var application = typeof(UserPantry.Application.Queries.GetExpiringPantryItems).Assembly;
        var infrastructure = typeof(UserPantry.Infrastructure.PantryDbContext).Assembly;

        Assert.True(
            Types.InAssembly(domain)
                .That()
                .ResideInNamespace("UserPantry.Domain")
                .ShouldNot()
                .HaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult()
                .IsSuccessful);
        Assert.True(
            Types.InAssembly(application)
                .That()
                .ResideInNamespace("UserPantry.Application")
                .ShouldNot()
                .HaveDependencyOn("Microsoft.AspNetCore")
                .GetResult()
                .IsSuccessful);
        Assert.True(
            Types.InAssembly(infrastructure)
                .That()
                .ResideInNamespace("UserPantry.Infrastructure")
                .ShouldNot()
                .HaveDependencyOn("Microsoft.AspNetCore")
                .GetResult()
                .IsSuccessful);
    }
}
