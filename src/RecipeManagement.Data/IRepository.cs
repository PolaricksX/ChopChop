using System.Linq.Expressions;

namespace RecipeManagement.Data;

public interface IRepository<T> where T : class
{
    Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
    Task<T?> FindForUpdateAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
    Task AddAsync(T entity, CancellationToken cancellationToken);
    void Update(T entity);
    void Remove(T entity);
}

public interface IRepositoryFactory
{
    IRepository<T> Create<T>() where T : class;
    TRepository CreateCustom<TRepository>() where TRepository : class;
}

public interface IRepositoryProvider
{
    IRepository<T> GetRepository<T>() where T : class;
    TRepository GetCustomRepository<TRepository>() where TRepository : class;
}
