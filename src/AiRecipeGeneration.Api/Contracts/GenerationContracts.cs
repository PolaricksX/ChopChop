using AiRecipeGeneration.Domain;

namespace AiRecipeGeneration.Api.Contracts;

public sealed record CreateGenerationRequest(
    string Prompt,
    bool IncludePantry = false,
    IReadOnlyList<string>? PantryItems = null);

public sealed record GenerationResponse(
    Guid Id,
    GenerationStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    RecipeDraft? Draft,
    string? FailureCode);
