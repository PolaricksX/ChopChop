using RecipeManagement.Domain.Entities;
using RecipeManagement.Domain.Enums;

namespace RecipeManagement.Data.Repositories;

public sealed record RecipeSearchCriteria(
    string? Text = null,
    IReadOnlyCollection<string>? Tags = null,
    IReadOnlyCollection<string>? DietaryLabels = null,
    int? MaxTotalMinutes = null,
    int? Servings = null,
    IReadOnlyCollection<Allergen>? ExcludedAllergens = null,
    IReadOnlyCollection<string>? AvailableEquipment = null,
    string Sort = "updated",
    bool Descending = true,
    int Page = 1,
    int PageSize = 20);

public sealed record PagedRecipes(
    IReadOnlyList<Recipe> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int AllergenExcludedCount = 0,
    int PreferenceExcludedCount = 0);

public interface IRecipeQueries
{
    Task<PagedRecipes> GetDraftsAsync(Guid ownerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Recipe?> GetOwnedDraftAsync(Guid ownerId, Guid recipeId, CancellationToken cancellationToken);
    Task<Recipe?> GetOwnedForUpdateAsync(Guid ownerId, Guid recipeId, CancellationToken cancellationToken);
    Task<Recipe?> GetPublishedAsync(Guid recipeId, CancellationToken cancellationToken);
    Task<Recipe?> GetPublishedVersionAsync(Guid recipeId, int version, CancellationToken cancellationToken);
    Task<PagedRecipes> SearchPublishedAsync(RecipeSearchCriteria criteria, CancellationToken cancellationToken);
}
