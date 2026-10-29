using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using UserService.Data.Context;

namespace UserService.Data;

/// <summary>Lets <c>dotnet ef</c> build the model without booting the API. The connection is never opened to add a migration.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UserServiceDbContext>
{
    public UserServiceDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__UserService")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ChopChopUserService;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<UserServiceDbContext>().UseSqlServer(connection).Options;
        return new UserServiceDbContext(options);
    }
}
