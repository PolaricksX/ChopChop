using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeManagement.Domain.Entities;

namespace RecipeManagement.Data.Configurations;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> entity)
    {
        entity.ToTable("Ingredients"); entity.HasKey("RecipeId", "Version", "Position");
        entity.Property<int>("Position"); entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); entity.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Quantity).HasPrecision(18, 3);
    }
}

public sealed class InstructionStepConfiguration : IEntityTypeConfiguration<InstructionStep>
{
    public void Configure(EntityTypeBuilder<InstructionStep> entity)
    {
        entity.ToTable("InstructionSteps"); entity.HasKey("RecipeId", "Version", nameof(InstructionStep.Order));
        entity.Property(x => x.Content).HasMaxLength(2_000).IsRequired();
    }
}

public sealed class NutritionEstimateConfiguration : IEntityTypeConfiguration<NutritionEstimate>
{
    public void Configure(EntityTypeBuilder<NutritionEstimate> entity)
    {
        entity.Property(x => x.Calories).HasPrecision(18, 3); entity.Property(x => x.ProteinGrams).HasPrecision(18, 3);
        entity.Property(x => x.CarbohydratesGrams).HasPrecision(18, 3); entity.Property(x => x.FatGrams).HasPrecision(18, 3);
        entity.Property(x => x.FiberGrams).HasPrecision(18, 3); entity.Property(x => x.SodiumMilligrams).HasPrecision(18, 3);
    }
}

public sealed class EquipmentRequirementConfiguration : IEntityTypeConfiguration<EquipmentRequirement>
{
    public void Configure(EntityTypeBuilder<EquipmentRequirement> entity)
    {
        entity.ToTable("EquipmentRequirements"); entity.HasKey("RecipeId", "Version", "Position");
        entity.Property<int>("Position"); entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); entity.Property(x => x.MatchingMetadata).HasMaxLength(500);
    }
}
