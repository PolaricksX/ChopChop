using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InteractiveReview.Data.Repositories;

internal sealed class EfRepository<T>(DbContext context) : IRepository<T> where T : class
{
    public async Task AddAsync(T entity, CancellationToken cancellationToken) =>
        await context.Set<T>().AddAsync(entity, cancellationToken);

    public void Update(T entity) => context.Set<T>().Update(entity);

    public void Remove(T entity) => context.Set<T>().Remove(entity);
}

internal sealed class RepositoryProvider(InteractiveReview.Data.Context.InteractiveReviewDbContext context)
    : IRepositoryProvider
{
    public IRepository<T> Create<T>() where T : class => new EfRepository<T>(context);
}

internal sealed class RepositoryFactory(
    IServiceProvider services,
    IRepositoryProvider provider) : IRepositoryFactory
{
    public IRepository<T> Create<T>() where T : class =>
        services.GetService<IRepository<T>>() ?? provider.Create<T>();

    public T CreateCustom<T>() where T : class => services.GetRequiredService<T>();
}

internal sealed class UnitOfWork(
    InteractiveReview.Data.Context.InteractiveReviewDbContext context,
    IRepositoryFactory repositoryFactory) : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : class => repositoryFactory.Create<T>();

    public T CustomRepository<T>() where T : class => repositoryFactory.CreateCustom<T>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
