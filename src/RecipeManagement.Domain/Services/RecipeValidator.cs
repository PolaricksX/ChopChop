using RecipeManagement.Domain.Entities;
using RecipeManagement.Domain.Enums;
using RecipeManagement.Domain.Exceptions;

namespace RecipeManagement.Domain.Services;

public static class RecipeValidator
{
    public static void ValidateVersion(RecipeVersion version)
    {
        if (version.Version < 1) throw new RecipeValidationException("Recipe versions start at 1.");
        if (string.IsNullOrWhiteSpace(version.Title) || version.Title.Length > 200) throw new RecipeValidationException("A recipe title of 1 to 200 characters is required.");
        if (version.Servings < 1) throw new RecipeValidationException("Servings must be positive.");
        if (version.Ingredients.Count == 0) throw new RecipeValidationException("At least one ingredient is required.");
        if (version.InstructionSteps.Count == 0) throw new RecipeValidationException("At least one instruction step is required.");
        if (version.InstructionSteps.Select(x => x.Order).Distinct().Count() != version.InstructionSteps.Count || version.InstructionSteps.Any(x => x.Order < 1))
            throw new RecipeValidationException("Instruction steps must have unique positive order values.");
        if (version.AllergenStatus == AllergenStatus.Unknown && version.Allergens.Count != 0)
            throw new RecipeValidationException("Unknown allergen status cannot include declared allergens.");
        if (version.Allergens.Any(a => !Enum.IsDefined(typeof(Allergen), a))) throw new RecipeValidationException("An invalid allergen was supplied.");
        if (version.TotalMinutes < 0 || version.PrepMinutes < 0 || version.CookMinutes < 0)
            throw new RecipeValidationException("Recipe times cannot be negative.");
    }

    public static void ValidateOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty) throw new RecipeValidationException("An owner identifier is required.");
    }
}
