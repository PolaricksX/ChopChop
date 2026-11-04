using Cookbook.Domain.Exceptions;

namespace Cookbook.Domain.Services;

public static class SavedRecipeRules
{
    public static void ValidateFolderId(Guid folderId)
    {
        if (folderId == Guid.Empty)
            throw new CookbookValidationException("A folder identifier is required.");
    }

    public static void ValidateRecipeId(Guid recipeId)
    {
        if (recipeId == Guid.Empty)
            throw new CookbookValidationException("A recipe identifier is required.");
    }

    public static void ValidateRecipeVersion(int recipeVersion)
    {
        if (recipeVersion < 1)
            throw new CookbookValidationException("Recipe versions must be positive.");
    }
}
