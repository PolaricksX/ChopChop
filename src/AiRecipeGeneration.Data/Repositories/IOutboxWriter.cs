namespace AiRecipeGeneration.Data.Repositories;

public interface IOutboxWriter
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);
}
