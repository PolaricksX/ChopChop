using Cookbook.Domain.Exceptions;
using Cookbook.Domain.Services;

namespace Cookbook.Domain.Entities;

public sealed class Folder
{
    private readonly List<SavedRecipe> savedRecipes = [];

    private Folder() { }

    private Folder(Guid id, Guid ownerId, string name, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new CookbookValidationException("A folder identifier is required.");

        FolderRules.ValidateOwner(ownerId);
        Id = id;
        OwnerId = ownerId;
        Name = FolderRules.NormalizeName(name);
        CreatedAtUtc = FolderRules.NormalizeTimestamp(createdAtUtc, "creation");
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyList<SavedRecipe> SavedRecipes => savedRecipes;

    public static Folder Create(Guid ownerId, string name, DateTimeOffset createdAtUtc, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), ownerId, name, createdAtUtc);

    public void AddSavedRecipe(SavedRecipe savedRecipe)
    {
        ArgumentNullException.ThrowIfNull(savedRecipe);

        if (savedRecipe.FolderId != Id)
            throw new CookbookValidationException("The saved recipe belongs to a different folder.");

        if (savedRecipes.Any(x =>
            x.RecipeId == savedRecipe.RecipeId && x.RecipeVersion == savedRecipe.RecipeVersion))
            return;

        savedRecipes.Add(savedRecipe);
    }

    public SavedRecipe RemoveSavedRecipe(Guid recipeId, int recipeVersion)
    {
        SavedRecipeRules.ValidateRecipeId(recipeId);
        SavedRecipeRules.ValidateRecipeVersion(recipeVersion);

        var savedRecipe = savedRecipes.FirstOrDefault(x =>
            x.RecipeId == recipeId && x.RecipeVersion == recipeVersion);
        if (savedRecipe is null)
            throw new CookbookNotFoundException("The saved recipe was not found in the folder.");

        savedRecipes.Remove(savedRecipe);
        return savedRecipe;
    }
}
