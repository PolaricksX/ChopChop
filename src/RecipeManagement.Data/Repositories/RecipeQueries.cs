using Microsoft.EntityFrameworkCore;
using RecipeManagement.Data.Context;
using RecipeManagement.Domain.Entities;
using RecipeManagement.Domain.Enums;

namespace RecipeManagement.Data.Repositories;

internal sealed class RecipeQueries(RecipeManagementDbContext context) : IRecipeQueries
{
    private IQueryable<Recipe> WithContent(bool publishedOnly = false)
    {
        var query = context.Recipes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(recipe => publishedOnly
                ? recipe.Versions.Where(version => version.Status == RecipeStatus.Published)
                : recipe.Versions)
            .ThenInclude(version => version.Ingredients)
            .Include(recipe => publishedOnly
                ? recipe.Versions.Where(version => version.Status == RecipeStatus.Published)
                : recipe.Versions)
            .ThenInclude(version => version.InstructionSteps)
            .Include(recipe => publishedOnly
                ? recipe.Versions.Where(version => version.Status == RecipeStatus.Published)
                : recipe.Versions)
            .ThenInclude(version => version.EquipmentRequirements);

        return query;
    }

    public async Task<PagedRecipes> GetDraftsAsync(
        Guid ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var draftQuery = context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.OwnerId == ownerId &&
                (recipe.Status == RecipeStatus.Draft || recipe.Versions.Any(version => version.Status == RecipeStatus.Draft)));
        var totalCount = await draftQuery.CountAsync(cancellationToken);
        var ids = await draftQuery
            .OrderByDescending(recipe => recipe.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(recipe => recipe.Id)
            .ToListAsync(cancellationToken);

        var items = await WithContent()
            .Where(recipe => ids.Contains(recipe.Id))
            .ToListAsync(cancellationToken);
        var orderedItems = ids
            .Join(items, id => id, recipe => recipe.Id, (_, recipe) => recipe)
            .ToList();

        return new PagedRecipes(orderedItems, page, pageSize, totalCount);
    }

    public Task<Recipe?> GetOwnedDraftAsync(Guid ownerId, Guid recipeId, CancellationToken cancellationToken) =>
        WithContent().SingleOrDefaultAsync(
            recipe => recipe.Id == recipeId &&
                recipe.OwnerId == ownerId &&
                recipe.Versions.Any(version => version.Status == RecipeStatus.Draft),
            cancellationToken);

    public Task<Recipe?> GetPublishedAsync(Guid recipeId, CancellationToken cancellationToken) =>
        WithContent(publishedOnly: true).SingleOrDefaultAsync(
            recipe => recipe.Id == recipeId && recipe.Status == RecipeStatus.Published,
            cancellationToken);

    public Task<Recipe?> GetPublishedVersionAsync(Guid recipeId, int version, CancellationToken cancellationToken) =>
        WithContent().SingleOrDefaultAsync(
            recipe => recipe.Id == recipeId &&
                recipe.Status == RecipeStatus.Published &&
                recipe.Versions.Any(candidate =>
                    candidate.Version == version &&
                    (candidate.Status == RecipeStatus.Published || candidate.Status == RecipeStatus.Superseded)),
            cancellationToken);

    public async Task<PagedRecipes> SearchPublishedAsync(
        RecipeSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);
        var text = NormalizeText(criteria.Text);

        var candidates = await (
            from recipe in context.Recipes.AsNoTracking()
            from version in context.RecipeVersions.AsNoTracking()
            where recipe.Status == RecipeStatus.Published &&
                version.Status == RecipeStatus.Published &&
                EF.Property<Guid>(version, "RecipeId") == recipe.Id &&
                (text == null || version.Title.Contains(text) || version.Description.Contains(text)) &&
                (criteria.MaxTotalMinutes == null || version.TotalMinutes <= criteria.MaxTotalMinutes) &&
                (criteria.Servings == null || version.Servings == criteria.Servings)
            select new PublishedCandidate(
                recipe.Id,
                version.Version,
                version.Title,
                version.TotalMinutes,
                version.Servings,
                version.PublishedAtUtc,
                version.AllergenStatus,
                version.Allergens,
                version.Tags,
                version.DietaryLabels))
            .ToListAsync(cancellationToken);

        var equipmentByRecipe = await LoadEquipmentAsync(candidates, cancellationToken);
        var allergenExcludedCount = 0;
        var preferenceExcludedCount = 0;
        var matchingCandidates = new List<PublishedCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var allergenExcluded = criteria.ExcludedAllergens?.Count > 0 &&
                (candidate.AllergenStatus == AllergenStatus.Unknown ||
                 criteria.ExcludedAllergens.Any(candidate.Allergens.Contains));
            if (allergenExcluded)
            {
                allergenExcludedCount++;
            }

            var preferenceExcluded =
                (criteria.Tags?.Count > 0 && !criteria.Tags.All(tag => candidate.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))) ||
                (criteria.DietaryLabels?.Count > 0 && !criteria.DietaryLabels.All(label => candidate.DietaryLabels.Contains(label, StringComparer.OrdinalIgnoreCase)));
            if (preferenceExcluded)
            {
                preferenceExcludedCount++;
            }

            var equipmentExcluded = criteria.AvailableEquipment?.Count > 0 &&
                equipmentByRecipe[candidate.RecipeId].Any(requirement =>
                    requirement.Required &&
                    !criteria.AvailableEquipment.Contains(requirement.Name, StringComparer.OrdinalIgnoreCase));

            if (!allergenExcluded && !preferenceExcluded && !equipmentExcluded)
            {
                matchingCandidates.Add(candidate);
            }
        }

        var orderedCandidates = OrderCandidates(matchingCandidates, criteria).ToList();
        var totalCount = orderedCandidates.Count;
        var pageCandidates = orderedCandidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        var pageIds = pageCandidates.Select(candidate => candidate.RecipeId).ToList();
        var items = await WithContent(publishedOnly: true)
            .Where(recipe => pageIds.Contains(recipe.Id))
            .ToListAsync(cancellationToken);
        var orderedItems = pageIds
            .Join(items, id => id, recipe => recipe.Id, (_, recipe) => recipe)
            .ToList();

        return new PagedRecipes(
            orderedItems,
            page,
            pageSize,
            totalCount,
            allergenExcludedCount,
            preferenceExcludedCount);
    }

    private async Task<Dictionary<Guid, List<EquipmentFilter>>> LoadEquipmentAsync(
        IReadOnlyCollection<PublishedCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var candidateVersions = candidates
            .GroupBy(candidate => candidate.RecipeId)
            .ToDictionary(group => group.Key, group => group.Single().Version);
        var recipeIds = candidateVersions.Keys.ToList();
        var equipment = await context.Set<EquipmentRequirement>()
            .AsNoTracking()
            .Where(requirement => recipeIds.Contains(EF.Property<Guid>(requirement, "RecipeId")))
            .Select(requirement => new
            {
                RecipeId = EF.Property<Guid>(requirement, "RecipeId"),
                Version = EF.Property<int>(requirement, "Version"),
                requirement.Name,
                requirement.Required
            })
            .ToListAsync(cancellationToken);

        return recipeIds.ToDictionary(
            recipeId => recipeId,
            recipeId => equipment
                .Where(requirement => requirement.RecipeId == recipeId && requirement.Version == candidateVersions[recipeId])
                .Select(requirement => new EquipmentFilter(requirement.Name, requirement.Required))
                .ToList());
    }

    private static IEnumerable<PublishedCandidate> OrderCandidates(
        IEnumerable<PublishedCandidate> candidates,
        RecipeSearchCriteria criteria) =>
        (criteria.Sort.ToLowerInvariant(), criteria.Descending) switch
        {
            ("title", true) => candidates.OrderByDescending(candidate => candidate.Title),
            ("title", false) => candidates.OrderBy(candidate => candidate.Title),
            ("totalminutes", true) => candidates.OrderByDescending(candidate => candidate.TotalMinutes),
            ("totalminutes", false) => candidates.OrderBy(candidate => candidate.TotalMinutes),
            ("servings", true) => candidates.OrderByDescending(candidate => candidate.Servings),
            ("servings", false) => candidates.OrderBy(candidate => candidate.Servings),
            ("publishedat", true) => candidates.OrderByDescending(candidate => candidate.PublishedAtUtc),
            ("publishedat", false) => candidates.OrderBy(candidate => candidate.PublishedAtUtc),
            (_, true) => candidates.OrderByDescending(candidate => candidate.PublishedAtUtc),
            _ => candidates.OrderBy(candidate => candidate.PublishedAtUtc)
        };

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, 200)];
    }

    private sealed record PublishedCandidate(
        Guid RecipeId,
        int Version,
        string Title,
        int TotalMinutes,
        int Servings,
        DateTimeOffset? PublishedAtUtc,
        AllergenStatus AllergenStatus,
        IReadOnlyList<Allergen> Allergens,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> DietaryLabels);

    private sealed record EquipmentFilter(string Name, bool Required);
}
