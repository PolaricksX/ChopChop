using Cookbook.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cookbook.Data;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CookbookDbContext>
{
    public CookbookDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Cookbook")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ChopChopCookbook;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<CookbookDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new CookbookDbContext(options);
    }
}
