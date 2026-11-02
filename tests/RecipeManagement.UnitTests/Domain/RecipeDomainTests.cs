using RecipeManagement.Domain.Entities;
using RecipeManagement.Domain.Enums;
using RecipeManagement.Domain.Exceptions;

namespace RecipeManagement.UnitTests.Domain;

public sealed class RecipeDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RecipeVersion Version(string title = "Soup", int servings = 2,
        IEnumerable<Ingredient>? ingredients = null, IEnumerable<InstructionStep>? steps = null) =>
        new(title, "Description", servings, ingredients ?? [new Ingredient("Carrot", 1, "piece")],
            steps ?? [new InstructionStep(1, "Cook")], createdAtUtc: Now);

    private static Recipe NewRecipe() => Recipe.Create(Guid.NewGuid(), Version(), Now);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_title_is_rejected(string title) =>
        Assert.Throws<RecipeValidationException>(() => Recipe.Create(Guid.NewGuid(), Version(title), Now));

    [Fact]
    public void Empty_ingredients_are_rejected() =>
        Assert.Throws<RecipeValidationException>(() => Recipe.Create(Guid.NewGuid(), Version(ingredients: []), Now));

    [Fact]
    public void Empty_steps_are_rejected() =>
        Assert.Throws<RecipeValidationException>(() => Recipe.Create(Guid.NewGuid(), Version(steps: []), Now));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_servings_are_rejected(int servings) =>
        Assert.Throws<RecipeValidationException>(() => Recipe.Create(Guid.NewGuid(), Version(servings: servings), Now));

    [Fact]
    public void Ingredient_requires_a_unit() =>
        Assert.Throws<ArgumentException>(() => new Ingredient("Flour", 1, ""));

    [Fact]
    public void New_recipe_starts_at_version_one_and_can_be_published()
    {
        var recipe = NewRecipe();

        Assert.Equal(1, recipe.CurrentVersion);
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
        Assert.Equal(RecipePublicationType.FirstPublication, recipe.Publish(Now.AddMinutes(1)));
        Assert.Equal(RecipeStatus.Published, recipe.Status);
        Assert.Equal(1, recipe.PublishedVersion);
    }

    [Fact]
    public void Published_recipe_edit_gets_next_version_and_can_be_published()
    {
        var recipe = NewRecipe();
        recipe.Publish(Now);

        var draft = recipe.CreateDraftVersion(Version("Edited soup"), Now.AddMinutes(1));
        Assert.Equal(2, draft.Version);
        Assert.True(recipe.HasDraft);
        Assert.Equal(RecipePublicationType.Update, recipe.Publish(Now.AddMinutes(2)));
        Assert.Equal(2, recipe.PublishedVersion);
        Assert.Equal(RecipeStatus.Superseded, recipe.Versions.Single(x => x.Version == 1).Status);
    }

    [Fact]
    public void Only_one_draft_is_allowed_and_published_content_is_not_modifiable()
    {
        var recipe = NewRecipe();
        recipe.Publish(Now);
        recipe.CreateDraftVersion(Version("Draft"), Now.AddMinutes(1));

        Assert.Throws<RecipeConflictException>(() => recipe.CreateDraftVersion(Version("Second"), Now.AddMinutes(2)));
        recipe.Publish(Now.AddMinutes(3));
        Assert.Throws<RecipeLifecycleException>(() => recipe.ReplaceDraftContent(Version("Changed"), Now.AddMinutes(4)));
    }

    [Fact]
    public void Unpublish_and_republish_are_valid_lifecycle_transitions()
    {
        var recipe = NewRecipe();
        recipe.Publish(Now);
        recipe.Unpublish(Now.AddMinutes(1));
        Assert.Equal(RecipeStatus.Unpublished, recipe.Status);

        Assert.Equal(RecipePublicationType.Republication, recipe.Publish(Now.AddMinutes(2)));
        Assert.Equal(RecipeStatus.Published, recipe.Status);
    }

    [Fact]
    public void Invalid_transitions_and_missing_drafts_are_rejected()
    {
        var recipe = NewRecipe();
        Assert.Throws<RecipeLifecycleException>(() => recipe.Unpublish(Now));
        recipe.Publish(Now);
        Assert.Throws<RecipeLifecycleException>(() => recipe.Publish(Now.AddMinutes(1)));
    }

    [Fact]
    public void Draft_can_be_discarded_after_publication_but_not_before()
    {
        var recipe = NewRecipe();
        Assert.Throws<RecipeLifecycleException>(() => recipe.DiscardDraft(Now));
        recipe.Publish(Now);
        recipe.CreateDraftVersion(Version("Draft"), Now.AddMinutes(1));
        recipe.DiscardDraft(Now.AddMinutes(2));

        Assert.False(recipe.HasDraft);
        Assert.Equal(1, recipe.PublishedVersion);
    }
}
