using RecipeManagement.Domain.Entities;

namespace RecipeManagement.Api.Contracts;

public static class RecipeMappings
{
    public static RecipeVersion ToDomain(this RecipeContentRequest request, DateTimeOffset now) => new(
        request.Title, request.Description, request.Servings,
        request.Ingredients.Select(x => new Ingredient(x.Name, x.Quantity, x.Unit)),
        request.Steps.Select(x => new InstructionStep(x.Order, x.Content)), request.AllergenStatus,
        request.Allergens, request.Tags, request.DietaryLabels,
        request.Nutrition is null ? null : new NutritionEstimate(request.Nutrition.Calories, request.Nutrition.ProteinGrams, request.Nutrition.CarbohydratesGrams, request.Nutrition.FatGrams, request.Nutrition.FiberGrams, request.Nutrition.SodiumMilligrams),
        request.Equipment?.Select(x => new EquipmentRequirement(x.Name, x.Required, x.MatchingMetadata)),
        request.PrepMinutes, request.CookMinutes, request.TotalMinutes, request.IsAiGenerated, now);

    public static RecipeVersionResponse ToResponse(this RecipeVersion version, Guid recipeId) => new(
        recipeId, version.Version, version.Title, version.Description, version.Servings,
        version.Ingredients.Select(x => new IngredientResponse(x.Name, x.Quantity, x.Unit)).ToArray(),
        version.InstructionSteps.Select(x => new InstructionStepResponse(x.Order, x.Content)).ToArray(),
        version.AllergenStatus, version.Allergens.ToArray(), version.Tags.ToArray(), version.DietaryLabels.ToArray(),
        version.Nutrition is null ? null : new NutritionResponse(version.Nutrition.Calories, version.Nutrition.ProteinGrams, version.Nutrition.CarbohydratesGrams, version.Nutrition.FatGrams, version.Nutrition.FiberGrams, version.Nutrition.SodiumMilligrams),
        version.EquipmentRequirements.Select(x => new EquipmentResponse(x.Name, x.Required, x.MatchingMetadata)).ToArray(),
        version.PrepMinutes, version.CookMinutes, version.TotalMinutes, version.IsAiGenerated, version.CreatedAtUtc, version.PublishedAtUtc);

    public static DraftRecipeResponse ToDraftResponse(this Recipe recipe) => new(recipe.Id, recipe.OwnerId, recipe.Status, recipe.Draft!.ToResponse(recipe.Id), recipe.UpdatedAtUtc);
    public static PublishedRecipeResponse ToPublishedResponse(this Recipe recipe, RecipeVersion version) => new(version.ToResponse(recipe.Id));
    public static RecipeSummaryResponse ToSummary(this Recipe recipe) { var v = recipe.Published!; return new(recipe.Id, v.Version, v.Title, v.Servings, v.TotalMinutes, v.PublishedAtUtc, v.AllergenStatus, v.Allergens.ToArray(), v.Tags.ToArray(), v.DietaryLabels.ToArray()); }
}
