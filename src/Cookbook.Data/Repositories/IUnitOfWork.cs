using Cookbook.Data;

namespace Cookbook.Data.Repositories;

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class;
    TRepository CustomRepository<TRepository>() where TRepository : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
