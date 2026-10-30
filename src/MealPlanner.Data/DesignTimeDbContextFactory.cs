using MealPlanner.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MealPlanner.Data;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MealPlannerDbContext>
{
    public MealPlannerDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ChopChop")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ChopChop;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<MealPlannerDbContext>().UseSqlServer(connection).Options;
        return new MealPlannerDbContext(options);
    }
}
