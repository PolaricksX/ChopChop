using Cookbook.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cookbook.Data.Configurations;

public sealed class SavedRecipeConfiguration : IEntityTypeConfiguration<SavedRecipe>
{
    public void Configure(EntityTypeBuilder<SavedRecipe> entity)
    {
        entity.ToTable("SavedRecipes");
        entity.HasKey(savedRecipe => new
        {
            savedRecipe.FolderId,
            savedRecipe.RecipeId,
            savedRecipe.RecipeVersion
        });
        entity.Property(savedRecipe => savedRecipe.FolderId).IsRequired();
        entity.Property(savedRecipe => savedRecipe.RecipeId).IsRequired();
        entity.Property(savedRecipe => savedRecipe.RecipeVersion).IsRequired();
        entity.Property(savedRecipe => savedRecipe.SavedAtUtc).IsRequired();
        entity.HasIndex(savedRecipe => new
        {
            savedRecipe.RecipeId,
            savedRecipe.RecipeVersion
        });
    }
}
