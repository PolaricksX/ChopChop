using AiRecipeGeneration.Domain;

namespace AiRecipeGeneration.Data.Providers;

public interface IAiRecipeProvider
{
    Task<RecipeDraft> GenerateAsync(
        GenerationRequest request,
        string correlationId,
        CancellationToken cancellationToken);
}
