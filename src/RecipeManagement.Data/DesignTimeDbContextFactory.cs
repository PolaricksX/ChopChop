using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RecipeManagement.Data.Context;

namespace RecipeManagement.Data;

/// <summary>Lets <c>dotnet ef</c> build the model without booting the API. The connection is never opened to add a migration.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RecipeManagementDbContext>
{
    public RecipeManagementDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RecipeManagement")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ChopChopRecipeManagement;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RecipeManagementDbContext>().UseSqlServer(connection).Options;
        return new RecipeManagementDbContext(options);
    }
}
