using AiRecipeGeneration.Domain;

namespace AiRecipeGeneration.UnitTests;

public sealed class GenerationRequestTests
{
    [Fact]
    public void Create_StoresAnOptedInPantryAndStartsQueued()
    {
        var ownerId = Guid.NewGuid();

        var request = GenerationRequest.Create(
            Guid.NewGuid(),
            ownerId,
            "request-1",
            "Create a quick dinner",
            true,
            ["rice", "beans"],
            "correlation-1",
            DateTimeOffset.UtcNow);

        Assert.Equal(GenerationStatus.Queued, request.Status);
        Assert.Equal(ownerId, request.OwnerId);
        Assert.Equal(["rice", "beans"], request.PantryItems);
    }

    [Fact]
    public void Create_DoesNotStorePantryUnlessUserOptsIn()
    {
        var request = GenerationRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "request-2",
            "Create a quick dinner",
            false,
            ["private item"],
            "correlation-2",
            DateTimeOffset.UtcNow);

        Assert.Empty(request.PantryItems);
    }

    [Fact]
    public void Complete_RequiresValidatedRecipeAndProcessingState()
    {
        var request = CreateRequest();

        Assert.Throws<InvalidOperationException>(() => request.Complete(ValidDraft(), DateTimeOffset.UtcNow));
        request.MarkProcessing(DateTimeOffset.UtcNow);
        request.Complete(ValidDraft(), DateTimeOffset.UtcNow);

        Assert.Equal(GenerationStatus.Completed, request.Status);
        Assert.True(request.Draft?.IsAiGenerated);
        Assert.True(request.Draft?.NutritionIsEstimate);
    }

    [Fact]
    public void Confirm_RequiresTheOwnerAndACompletedDraft()
    {
        var request = CreateRequest();
        request.MarkProcessing(DateTimeOffset.UtcNow);
        request.Complete(ValidDraft(), DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => request.Confirm(Guid.NewGuid()));
        request.Confirm(request.OwnerId);

        Assert.Equal(GenerationStatus.Confirmed, request.Status);
    }

    [Fact]
    public void Validator_RejectsMissingAllergenDeclaration()
    {
        var draft = ValidDraft() with { Allergens = null! };

        Assert.False(RecipeDraftValidator.TryValidate(draft, out _));
    }

    [Fact]
    public void DomainAssembly_DoesNotDependOnDataOrWebFrameworks()
    {
        var references = typeof(GenerationRequest).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AiRecipeGeneration.Data", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("Microsoft.AspNetCore", references);
    }

    private static GenerationRequest CreateRequest() =>
        GenerationRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "request-3",
            "Create a quick dinner",
            false,
            null,
            "correlation-3",
            DateTimeOffset.UtcNow);

    private static RecipeDraft ValidDraft() =>
        new(
            "Vegetable rice",
            "A simple rice dish.",
            2,
            10,
            20,
            [new RecipeIngredient("Rice", 1, "cup", [])],
            ["Rinse the rice.", "Cook until tender."],
            [],
            [],
            new NutritionEstimate(350, 8, 60, 5),
            true,
            true);
}
