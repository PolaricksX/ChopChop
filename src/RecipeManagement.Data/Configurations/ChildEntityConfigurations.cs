using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeManagement.Domain.Entities;

namespace RecipeManagement.Data.Configurations;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> entity)
    {
        entity.ToTable("Ingredients");
        entity.Property<Guid>("RecipeId").IsRequired();
        entity.Property<int>("Version").IsRequired();
        entity.Property(x => x.Position).IsRequired();
        entity.HasKey("RecipeId", "Version", nameof(Ingredient.Position));
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); entity.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Quantity).HasPrecision(18, 3);
    }
}

public sealed class InstructionStepConfiguration : IEntityTypeConfiguration<InstructionStep>
{
    public void Configure(EntityTypeBuilder<InstructionStep> entity)
    {
        entity.ToTable("InstructionSteps");
        entity.Property<Guid>("RecipeId").IsRequired();
        entity.Property<int>("Version").IsRequired();
        entity.HasKey("RecipeId", "Version", nameof(InstructionStep.Order));
        entity.Property(x => x.Content).HasMaxLength(2_000).IsRequired();
    }
}

public sealed class EquipmentRequirementConfiguration : IEntityTypeConfiguration<EquipmentRequirement>
{
    public void Configure(EntityTypeBuilder<EquipmentRequirement> entity)
    {
        entity.ToTable("EquipmentRequirements");
        entity.Property<Guid>("RecipeId").IsRequired();
        entity.Property<int>("Version").IsRequired();
        entity.Property(x => x.Position).IsRequired();
        entity.HasKey("RecipeId", "Version", nameof(EquipmentRequirement.Position));
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); entity.Property(x => x.MatchingMetadata).HasMaxLength(500);
    }
}
