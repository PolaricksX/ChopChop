using System.Linq.Expressions;
using Cookbook.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Cookbook.Data.Repositories;

public sealed class EfRepository<T>(CookbookDbContext context) : IRepository<T> where T : class
{
    public Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) =>
        context.Set<T>().AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<T?> FindForUpdateAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) =>
        context.Set<T>().FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) =>
        context.Set<T>().AsNoTracking().AnyAsync(predicate, cancellationToken);

    public Task AddAsync(T entity, CancellationToken cancellationToken) =>
        context.Set<T>().AddAsync(entity, cancellationToken).AsTask();

    public void Update(T entity) => context.Set<T>().Update(entity);

    public void Remove(T entity) => context.Set<T>().Remove(entity);
}
