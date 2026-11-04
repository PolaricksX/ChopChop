using Cookbook.Data.Configurations;
using Cookbook.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cookbook.Data.Context;

public sealed class CookbookDbContext(DbContextOptions<CookbookDbContext> options) : DbContext(options)
{
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<SavedRecipe> SavedRecipes => Set<SavedRecipe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("cookbook");
        modelBuilder.ApplyConfiguration(new FolderConfiguration());
        modelBuilder.ApplyConfiguration(new SavedRecipeConfiguration());
    }
}
