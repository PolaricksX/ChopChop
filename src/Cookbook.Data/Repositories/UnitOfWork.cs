using Cookbook.Data.Context;

namespace Cookbook.Data.Repositories;

public sealed class UnitOfWork(CookbookDbContext context, IRepositoryProvider provider) : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : class => provider.GetRepository<T>();

    public TRepository CustomRepository<TRepository>() where TRepository : class =>
        provider.GetCustomRepository<TRepository>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
