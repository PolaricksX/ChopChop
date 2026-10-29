namespace AiRecipeGeneration.Domain;

public enum GenerationStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Confirmed
}
