namespace AiRecipeGeneration.Data.Repositories;

public interface IOutboxQueries
{
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int limit,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}
