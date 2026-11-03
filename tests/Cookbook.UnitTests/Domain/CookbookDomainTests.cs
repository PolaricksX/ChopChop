using Cookbook.Domain.Entities;
using Cookbook.Domain.Exceptions;

namespace Cookbook.UnitTests.Domain;

public sealed class CookbookDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Folder_name_is_required(string name) => Assert.Throws<CookbookValidationException>(() => Folder.Create(Guid.NewGuid(), name, Now));

    [Fact]
    public void Folder_name_is_trimmed()
    {
        var folder = Folder.Create(Guid.NewGuid(), "  Favorites  ", Now);
        Assert.Equal("Favorites", folder.Name);
    }

    [Fact]
    public void Folder_name_cannot_exceed_one_hundred_characters() => Assert.Throws<CookbookValidationException>(() => Folder.Create(Guid.NewGuid(), new string('x', 101), Now));

    [Fact]
    public void Owner_is_required() => Assert.Throws<CookbookValidationException>(() => Folder.Create(Guid.Empty, "Favorites", Now));

    [Fact]
    public void Recipe_id_must_be_non_empty() => Assert.Throws<CookbookValidationException>(() => SavedRecipe.Create(Guid.NewGuid(), Guid.Empty, 1, Now));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Recipe_version_must_be_positive(int version) => Assert.Throws<CookbookValidationException>(() => SavedRecipe.Create(Guid.NewGuid(), Guid.NewGuid(), version, Now));

    [Fact]
    public void Folder_can_add_a_saved_recipe()
    {
        var folder = Folder.Create(Guid.NewGuid(), "Favorites", Now);
        var saved = SavedRecipe.Create(folder.Id, Guid.NewGuid(), 2, Now);
        folder.AddSavedRecipe(saved);
        Assert.Single(folder.SavedRecipes);
        Assert.Same(saved, folder.SavedRecipes[0]);
    }

    [Fact]
    public void Adding_the_same_recipe_and_version_twice_is_a_no_op()
    {
        var folder = Folder.Create(Guid.NewGuid(), "Favorites", Now);
        var recipeId = Guid.NewGuid();
        folder.AddSavedRecipe(SavedRecipe.Create(folder.Id, recipeId, 1, Now));
        folder.AddSavedRecipe(SavedRecipe.Create(folder.Id, recipeId, 1, Now.AddMinutes(1)));
        Assert.Single(folder.SavedRecipes);
    }

    [Fact]
    public void Removing_a_recipe_not_in_the_folder_throws_not_found()
    {
        var folder = Folder.Create(Guid.NewGuid(), "Favorites", Now);
        Assert.Throws<CookbookNotFoundException>(() => folder.RemoveSavedRecipe(Guid.NewGuid(), 1));
    }
}
