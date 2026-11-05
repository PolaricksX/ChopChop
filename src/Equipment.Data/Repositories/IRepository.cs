using System.Linq.Expressions;

namespace Equipment.Data.Repositories;

public interface IRepository<T>
    where T : class
{
    Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken token);

    Task<T?> FindForUpdateAsync(Expression<Func<T, bool>> predicate, CancellationToken token);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken token);

    Task AddAsync(T entity, CancellationToken token);
}
