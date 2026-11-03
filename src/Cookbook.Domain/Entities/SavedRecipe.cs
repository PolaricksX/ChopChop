using Cookbook.Domain.Services;

namespace Cookbook.Domain.Entities;

public sealed class SavedRecipe
{
    private SavedRecipe() { }

    private SavedRecipe(Guid folderId, Guid recipeId, int recipeVersion, DateTimeOffset savedAtUtc)
    {
        SavedRecipeRules.ValidateFolderId(folderId);
        SavedRecipeRules.ValidateRecipeId(recipeId);
        SavedRecipeRules.ValidateRecipeVersion(recipeVersion);

        FolderId = folderId;
        RecipeId = recipeId;
        RecipeVersion = recipeVersion;
        SavedAtUtc = FolderRules.NormalizeTimestamp(savedAtUtc, "saved");
    }

    public Guid FolderId { get; private set; }
    public Guid RecipeId { get; private set; }
    public int RecipeVersion { get; private set; }
    public DateTimeOffset SavedAtUtc { get; private set; }

    public static SavedRecipe Create(Guid folderId, Guid recipeId, int recipeVersion, DateTimeOffset savedAtUtc) =>
        new(folderId, recipeId, recipeVersion, savedAtUtc);
}
