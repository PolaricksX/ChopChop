using RecipeManagement.Domain.Enums;

namespace RecipeManagement.Api.Contracts;

public sealed record IngredientRequest(string Name, decimal Quantity, string Unit);
public sealed record InstructionStepRequest(int Order, string Content);
public sealed record NutritionRequest(decimal Calories, decimal ProteinGrams, decimal CarbohydratesGrams, decimal FatGrams, decimal FiberGrams, decimal SodiumMilligrams);
public sealed record EquipmentRequest(string Name, bool Required = true, string? MatchingMetadata = null);

public record RecipeContentRequest(
    string Title,
    string Description,
    int Servings,
    IReadOnlyList<IngredientRequest> Ingredients,
    IReadOnlyList<InstructionStepRequest> Steps,
    AllergenStatus AllergenStatus = AllergenStatus.Unknown,
    IReadOnlyList<Allergen>? Allergens = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? DietaryLabels = null,
    NutritionRequest? Nutrition = null,
    IReadOnlyList<EquipmentRequest>? Equipment = null,
    int PrepMinutes = 0,
    int CookMinutes = 0,
    int TotalMinutes = 0,
    bool IsAiGenerated = false);

public sealed record CreateRecipeRequest : RecipeContentRequest
{
    public CreateRecipeRequest(string title, string description, int servings, IReadOnlyList<IngredientRequest> ingredients, IReadOnlyList<InstructionStepRequest> steps,
        AllergenStatus allergenStatus = AllergenStatus.Unknown, IReadOnlyList<Allergen>? allergens = null, IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? dietaryLabels = null, NutritionRequest? nutrition = null, IReadOnlyList<EquipmentRequest>? equipment = null,
        int prepMinutes = 0, int cookMinutes = 0, int totalMinutes = 0, bool isAiGenerated = false)
        : base(title, description, servings, ingredients, steps, allergenStatus, allergens, tags, dietaryLabels, nutrition, equipment, prepMinutes, cookMinutes, totalMinutes, isAiGenerated) { }
}

public sealed record UpdateRecipeRequest(int ExpectedVersion, RecipeContentRequest Content);

public sealed record IngredientResponse(string Name, decimal Quantity, string Unit);
public sealed record InstructionStepResponse(int Order, string Content);
public sealed record NutritionResponse(decimal Calories, decimal ProteinGrams, decimal CarbohydratesGrams, decimal FatGrams, decimal FiberGrams, decimal SodiumMilligrams);
public sealed record EquipmentResponse(string Name, bool Required, string? MatchingMetadata);

public record RecipeVersionResponse(
    Guid RecipeId, int Version, string Title, string Description, int Servings,
    IReadOnlyList<IngredientResponse> Ingredients, IReadOnlyList<InstructionStepResponse> Steps,
    AllergenStatus AllergenStatus, IReadOnlyList<Allergen> Allergens, IReadOnlyList<string> Tags,
    IReadOnlyList<string> DietaryLabels, NutritionResponse? Nutrition, IReadOnlyList<EquipmentResponse> Equipment,
    int PrepMinutes, int CookMinutes, int TotalMinutes, bool IsAiGenerated, DateTimeOffset CreatedAtUtc, DateTimeOffset? PublishedAtUtc);

public sealed record DraftRecipeResponse(Guid RecipeId, Guid OwnerId, RecipeStatus Status, RecipeVersionResponse Version, DateTimeOffset UpdatedAtUtc);
public sealed record PublishedRecipeResponse(RecipeVersionResponse Version);
public sealed record RecipeSummaryResponse(Guid RecipeId, int Version, string Title, int Servings, int TotalMinutes, DateTimeOffset? PublishedAtUtc, AllergenStatus AllergenStatus, IReadOnlyList<Allergen> Allergens, IReadOnlyList<string> Tags, IReadOnlyList<string> DietaryLabels);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int AllergenExcludedCount = 0, int PreferenceExcludedCount = 0);
