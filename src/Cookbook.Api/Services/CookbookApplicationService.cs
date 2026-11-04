using Cookbook.Api.Contracts;
using Cookbook.Data.Repositories;
using Cookbook.Domain.Entities;
using Cookbook.Domain.Exceptions;

namespace Cookbook.Api.Services;

public sealed class CookbookApplicationService(
    ICookbookQueries cookbookQueries,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<FolderResponse> CreateFolderAsync(Guid ownerId, CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var name = CookbookRequestValidator.ValidateAndNormalize(request);
        var folder = Folder.Create(ownerId, name, timeProvider.GetUtcNow());
        await unitOfWork.Repository<Folder>().AddAsync(folder, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return folder.ToResponse();
    }

    public async Task<IReadOnlyList<FolderResponse>> GetFoldersAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var folders = await cookbookQueries.GetFoldersAsync(ownerId, cancellationToken);
        return folders.Select(folder => folder.ToResponse()).ToArray();
    }

    public async Task DeleteFolderAsync(Guid ownerId, Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await GetTrackedFolderAsync(ownerId, folderId, cancellationToken);
        unitOfWork.Repository<Folder>().Remove(folder);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SavedRecipeResponse> AddSavedRecipeAsync(Guid ownerId, Guid folderId, SaveRecipeRequest request, CancellationToken cancellationToken)
    {
        CookbookRequestValidator.Validate(request);
        var folder = await GetTrackedFolderAsync(ownerId, folderId, cancellationToken);
        var existing = folder.SavedRecipes.FirstOrDefault(savedRecipe =>
            savedRecipe.RecipeId == request.RecipeId && savedRecipe.RecipeVersion == request.RecipeVersion);
        var savedRecipe = existing ?? SavedRecipe.Create(folder.Id, request.RecipeId, request.RecipeVersion, timeProvider.GetUtcNow());
        if (existing is null)
            folder.AddSavedRecipe(savedRecipe);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return savedRecipe.ToResponse();
    }

    public async Task<IReadOnlyList<SavedRecipeResponse>> GetSavedRecipesAsync(Guid ownerId, Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await cookbookQueries.GetFolderAsync(ownerId, folderId, cancellationToken)
            ?? throw new CookbookNotFoundException("The folder was not found.");
        return folder.SavedRecipes.Select(savedRecipe => savedRecipe.ToResponse()).ToArray();
    }

    public async Task RemoveSavedRecipeAsync(Guid ownerId, Guid folderId, Guid recipeId, int recipeVersion, CancellationToken cancellationToken)
    {
        CookbookRequestValidator.ValidateRecipeRoute(recipeId, recipeVersion);
        var folder = await GetTrackedFolderAsync(ownerId, folderId, cancellationToken);
        folder.RemoveSavedRecipe(recipeId, recipeVersion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Folder> GetTrackedFolderAsync(Guid ownerId, Guid folderId, CancellationToken cancellationToken) =>
        await cookbookQueries.GetFolderForUpdateAsync(ownerId, folderId, cancellationToken)
        ?? throw new CookbookNotFoundException("The folder was not found.");
}
