using RecipeManagement.Api.Contracts;
using RecipeManagement.Data.Repositories;
using RecipeManagement.Domain.Entities;
using RecipeManagement.Domain.Enums;
using RecipeManagement.Domain.Exceptions;

namespace RecipeManagement.Api.Services;

public sealed class RecipeApplicationService(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<DraftRecipeResponse> CreateDraftAsync(Guid ownerId, CreateRecipeRequest request, CancellationToken ct)
    {
        RecipeRequestValidator.Validate(request);
        var recipe = Recipe.Create(ownerId, request.ToDomain(timeProvider.GetUtcNow()), timeProvider.GetUtcNow());
        await unitOfWork.Repository<Recipe>().AddAsync(recipe, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return recipe.ToDraftResponse();
    }

    public async Task<PagedResponse<DraftRecipeResponse>> ListDraftsAsync(Guid ownerId, int page, int pageSize, CancellationToken ct)
    {
        RecipeRequestValidator.ValidatePage(page, pageSize);
        var result = await unitOfWork.CustomRepository<IRecipeQueries>().GetDraftsAsync(ownerId, page, pageSize, ct);
        return new(result.Items.Select(x => x.ToDraftResponse()).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<DraftRecipeResponse?> GetDraftAsync(Guid ownerId, Guid recipeId, CancellationToken ct) =>
        (await unitOfWork.CustomRepository<IRecipeQueries>().GetOwnedDraftAsync(ownerId, recipeId, ct))?.ToDraftResponse();

    public async Task UpdateDraftAsync(Guid ownerId, Guid recipeId, UpdateRecipeRequest request, CancellationToken ct)
    {
        RecipeRequestValidator.Validate(request.Content);
        var recipe = await unitOfWork.CustomRepository<IRecipeQueries>().GetOwnedForUpdateAsync(ownerId, recipeId, ct)
            ?? throw new KeyNotFoundException("Recipe draft was not found.");
        var draft = recipe.Draft!;
        var currentVersion = draft?.Version ?? recipe.PublishedVersion;
        if (currentVersion != request.ExpectedVersion) throw new RecipeConflictException("The recipe version is stale.");
        var replacement = request.Content.ToDomain(timeProvider.GetUtcNow());
        if (recipe.Status == RecipeStatus.Published && draft is null) recipe.CreateDraftVersion(replacement, timeProvider.GetUtcNow());
        else recipe.ReplaceDraftContent(replacement, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteDraftAsync(Guid ownerId, Guid recipeId, CancellationToken ct)
    {
        var recipe = await unitOfWork.CustomRepository<IRecipeQueries>().GetOwnedForUpdateAsync(ownerId, recipeId, ct)
            ?? throw new KeyNotFoundException("Recipe draft was not found.");
        if (recipe.Published is null) unitOfWork.Repository<Recipe>().Remove(recipe);
        else recipe.DiscardDraft(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PublishedRecipeResponse> PublishAsync(Guid ownerId, Guid recipeId, CancellationToken ct)
    {
        var recipe = await unitOfWork.CustomRepository<IRecipeQueries>().GetOwnedForUpdateAsync(ownerId, recipeId, ct)
            ?? throw new KeyNotFoundException("Recipe was not found.");
        recipe.Publish(timeProvider.GetUtcNow()); await unitOfWork.SaveChangesAsync(ct);
        return recipe.ToPublishedResponse(recipe.Published!);
    }

    public async Task UnpublishAsync(Guid ownerId, Guid recipeId, CancellationToken ct)
    {
        var recipe = await unitOfWork.CustomRepository<IRecipeQueries>().GetOwnedForUpdateAsync(ownerId, recipeId, ct)
            ?? throw new KeyNotFoundException("Recipe was not found.");
        recipe.Unpublish(timeProvider.GetUtcNow()); await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PublishedRecipeResponse?> GetPublishedAsync(Guid recipeId, CancellationToken ct) { var r = await unitOfWork.CustomRepository<IRecipeQueries>().GetPublishedAsync(recipeId, ct); return r is null ? null : r.ToPublishedResponse(r.Published!); }
    public async Task<PublishedRecipeResponse?> GetPublishedVersionAsync(Guid recipeId, int version, CancellationToken ct) { var r = await unitOfWork.CustomRepository<IRecipeQueries>().GetPublishedVersionAsync(recipeId, version, ct); var v = r?.Versions.SingleOrDefault(x => x.Version == version && x.Status is RecipeStatus.Published or RecipeStatus.Superseded); return v is null ? null : r!.ToPublishedResponse(v); }
    public async Task<PagedResponse<RecipeSummaryResponse>> SearchAsync(RecipeSearchCriteria criteria, CancellationToken ct) { var r = await unitOfWork.CustomRepository<IRecipeQueries>().SearchPublishedAsync(criteria, ct); return new(r.Items.Select(x => x.ToSummary()).ToArray(), r.Page, r.PageSize, r.TotalCount, r.AllergenExcludedCount, r.PreferenceExcludedCount); }

}
