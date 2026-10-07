using Microsoft.EntityFrameworkCore;
using UserPantry.Domain;

namespace UserPantry.Infrastructure;

public sealed class PantryDbContext(DbContextOptions<PantryDbContext> options) : DbContext(options)
{
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PantryItemConfiguration());
    }
}
