using Equipment.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Equipment.Data;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EquipmentDbContext>
{
    public EquipmentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Equipment")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Equipment to a SQL Server connection string for EF design-time commands.");
        var options = new DbContextOptionsBuilder<EquipmentDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new EquipmentDbContext(options);
    }
}
