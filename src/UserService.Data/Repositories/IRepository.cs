using System.Linq.Expressions;

namespace UserService.Data.Repositories;

/// <summary>Loads and stages one aggregate type. Never commits; the Unit of Work does.</summary>
public interface IRepository<T>
    where T : class
{
    /// <summary>Read-only lookup (no change tracking).</summary>
    Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken token);

    /// <summary>Tracked lookup for entities that will be modified.</summary>
    Task<T?> FindForUpdateAsync(Expression<Func<T, bool>> predicate, CancellationToken token);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken token);

    Task AddAsync(T entity, CancellationToken token);
}
