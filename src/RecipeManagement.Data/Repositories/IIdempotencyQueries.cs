namespace RecipeManagement.Data.Repositories;

public interface IIdempotencyQueries
{
    Task<IdempotencyRecord?> FindAsync(Guid ownerId, string key, CancellationToken cancellationToken);
    Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}
