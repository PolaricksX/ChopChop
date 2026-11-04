using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeManagement.Domain.Entities;
using RecipeManagement.Domain.Enums;

namespace RecipeManagement.Data.Configurations;

public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> entity)
    {
        entity.ToTable("Recipes");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        entity.Property(x => x.OwnerId).IsRequired();
        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.UpdatedAtUtc).IsRequired();
        entity.HasIndex(x => new { x.OwnerId, x.Status });
        entity.HasMany(x => x.Versions).WithOne().HasForeignKey("RecipeId").OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(x => x.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RecipeVersionConfiguration : IEntityTypeConfiguration<RecipeVersion>
{
    public void Configure(EntityTypeBuilder<RecipeVersion> entity)
    {
        entity.ToTable("RecipeVersions");
        entity.Property<Guid>("RecipeId").IsRequired();
        entity.HasKey("RecipeId", nameof(RecipeVersion.Version));
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(5_000).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        entity.Property(x => x.AllergenStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.IsAiGenerated).IsRequired();
        entity.Property<int>("ConcurrencyToken").IsConcurrencyToken().HasDefaultValue(0);
        entity.HasIndex("RecipeId", nameof(RecipeVersion.Status));
        entity.Property(x => x.Allergens).HasConversion(
            values => string.Join(',', values.Select(value => value.ToString())),
            value => string.IsNullOrEmpty(value) ? new List<Allergen>() : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Allergen>).ToList());
        entity.Property(x => x.Tags).HasConversion(values => string.Join('\u001f', values), value => value.Split('\u001f', StringSplitOptions.RemoveEmptyEntries).ToList());
        entity.Property(x => x.DietaryLabels).HasConversion(values => string.Join('\u001f', values), value => value.Split('\u001f', StringSplitOptions.RemoveEmptyEntries).ToList());
        entity.HasMany(x => x.Ingredients).WithOne().HasForeignKey("RecipeId", "Version").OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(x => x.InstructionSteps).WithOne().HasForeignKey("RecipeId", "Version").OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(x => x.EquipmentRequirements).WithOne().HasForeignKey("RecipeId", "Version").OnDelete(DeleteBehavior.Cascade);
        entity.OwnsOne(x => x.Nutrition, nutrition =>
        {
            nutrition.ToTable("NutritionEstimates");
            nutrition.Property<Guid>("RecipeId").IsRequired();
            nutrition.Property<int>("Version").IsRequired();
            nutrition.HasKey("RecipeId", "Version");
            nutrition.Property(x => x.Calories).HasPrecision(18, 3);
            nutrition.Property(x => x.ProteinGrams).HasPrecision(18, 3);
            nutrition.Property(x => x.CarbohydratesGrams).HasPrecision(18, 3);
            nutrition.Property(x => x.FatGrams).HasPrecision(18, 3);
            nutrition.Property(x => x.FiberGrams).HasPrecision(18, 3);
            nutrition.Property(x => x.SodiumMilligrams).HasPrecision(18, 3);
        });
    }
}
