using RecipeManagement.Domain.Entities;
using RecipeManagement.Domain.Enums;
using RecipeManagement.Domain.Exceptions;

namespace RecipeManagement.Domain.Services;

public static class RecipeLifecycle
{
    public static RecipePublicationType Publish(Recipe recipe, DateTimeOffset nowUtc) => recipe.Publish(nowUtc);
    public static void Unpublish(Recipe recipe, DateTimeOffset nowUtc) => recipe.Unpublish(nowUtc);

    public static void EnsureCanCreateDraft(Recipe recipe)
    {
        if (recipe.HasDraft) throw new RecipeConflictException("Only one draft version may exist for a recipe.");
    }

    public static void EnsureTransition(RecipeStatus current, RecipeStatus target)
    {
        var allowed = (current == RecipeStatus.Draft && target == RecipeStatus.Published)
            || (current == RecipeStatus.Published && target == RecipeStatus.Unpublished)
            || (current == RecipeStatus.Unpublished && target == RecipeStatus.Published)
            || (current == RecipeStatus.Published && target == RecipeStatus.Published);
        if (!allowed)
            throw new RecipeLifecycleException($"The transition from {current} to {target} is not permitted.");
    }
}
