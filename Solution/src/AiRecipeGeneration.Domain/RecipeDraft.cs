namespace AiRecipeGeneration.Domain;

public sealed record RecipeIngredient(
    string Name,
    decimal Quantity,
    string Unit,
    IReadOnlyList<string> Allergens);

public sealed record NutritionEstimate(
    decimal Calories,
    decimal ProteinGrams,
    decimal CarbohydrateGrams,
    decimal FatGrams);

public sealed record RecipeDraft(
    string Title,
    string Description,
    int Servings,
    int PrepMinutes,
    int CookMinutes,
    IReadOnlyList<RecipeIngredient> Ingredients,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Equipment,
    NutritionEstimate Nutrition,
    bool IsAiGenerated,
    bool NutritionIsEstimate);

public static class RecipeDraftValidator
{
    public static bool TryValidate(RecipeDraft? draft, out string error)
    {
        if (draft is null)
            return Invalid("Recipe output is required.", out error);

        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Length > 120)
            return Invalid("Title must contain 1 to 120 characters.", out error);

        if (string.IsNullOrWhiteSpace(draft.Description) || draft.Description.Length > 600)
            return Invalid("Description must contain 1 to 600 characters.", out error);

        if (draft.Servings is < 1 or > 20)
            return Invalid("Servings must be between 1 and 20.", out error);

        if (draft.PrepMinutes is < 0 or > 1440 || draft.CookMinutes is < 0 or > 1440)
            return Invalid("Preparation and cooking times must be between 0 and 1440 minutes.", out error);

        if (draft.Ingredients is null || draft.Ingredients.Count is < 1 or > 40)
            return Invalid("A recipe must have between 1 and 40 ingredients.", out error);

        if (draft.Ingredients.Any(ingredient =>
                ingredient is null ||
                string.IsNullOrWhiteSpace(ingredient.Name) ||
                ingredient.Name.Length > 120 ||
                string.IsNullOrWhiteSpace(ingredient.Unit) ||
                ingredient.Unit.Length > 40 ||
                ingredient.Quantity <= 0 ||
                ingredient.Quantity > 10_000 ||
                ingredient.Allergens is null ||
                ingredient.Allergens.Count > 20 ||
                ingredient.Allergens.Any(allergen => string.IsNullOrWhiteSpace(allergen) || allergen.Length > 80)))
        {
            return Invalid("Ingredient fields or allergen declarations are invalid.", out error);
        }

        if (draft.Steps is null ||
            draft.Steps.Count is < 1 or > 40 ||
            draft.Steps.Any(step => string.IsNullOrWhiteSpace(step) || step.Length > 1_000))
        {
            return Invalid("A recipe must have 1 to 40 valid preparation steps.", out error);
        }

        if (draft.Allergens is null ||
            draft.Allergens.Count > 30 ||
            draft.Allergens.Any(allergen => string.IsNullOrWhiteSpace(allergen) || allergen.Length > 80))
        {
            return Invalid("An explicit, valid allergen declaration is required.", out error);
        }

        if (draft.Equipment is null ||
            draft.Equipment.Count > 30 ||
            draft.Equipment.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 100))
        {
            return Invalid("Equipment declarations are invalid.", out error);
        }

        if (draft.Nutrition is null ||
            draft.Nutrition.Calories is < 0 or > 100_000 ||
            draft.Nutrition.ProteinGrams is < 0 or > 10_000 ||
            draft.Nutrition.CarbohydrateGrams is < 0 or > 10_000 ||
            draft.Nutrition.FatGrams is < 0 or > 10_000)
        {
            return Invalid("Nutrition estimates are invalid.", out error);
        }

        if (!draft.IsAiGenerated || !draft.NutritionIsEstimate)
            return Invalid("Generated recipes and nutrition estimates must be labelled.", out error);

        error = string.Empty;
        return true;
    }

    private static bool Invalid(string message, out string error)
    {
        error = message;
        return false;
    }
}
