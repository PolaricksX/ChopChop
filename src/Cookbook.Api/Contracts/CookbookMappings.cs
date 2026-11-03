using Cookbook.Domain.Entities;

namespace Cookbook.Api.Contracts;

public static class CookbookMappings
{
    public static FolderResponse ToResponse(this Folder folder) =>
        new(folder.Id, folder.Name, folder.CreatedAtUtc);

    public static SavedRecipeResponse ToResponse(this SavedRecipe savedRecipe) =>
        new(savedRecipe.FolderId, savedRecipe.RecipeId, savedRecipe.RecipeVersion, savedRecipe.SavedAtUtc);
}
