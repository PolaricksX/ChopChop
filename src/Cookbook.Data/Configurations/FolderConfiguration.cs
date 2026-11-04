using Cookbook.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cookbook.Data.Configurations;

public sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> entity)
    {
        entity.ToTable("Folders");
        entity.HasKey(folder => folder.Id);
        entity.Property(folder => folder.Id).IsRequired();
        entity.Property(folder => folder.OwnerId).IsRequired();
        entity.Property(folder => folder.Name).HasMaxLength(100).IsRequired();
        entity.Property(folder => folder.CreatedAtUtc).IsRequired();
        entity.HasIndex(folder => new { folder.OwnerId, folder.Name });

        entity.HasMany(folder => folder.SavedRecipes)
            .WithOne()
            .HasForeignKey(savedRecipe => savedRecipe.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(folder => folder.SavedRecipes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
