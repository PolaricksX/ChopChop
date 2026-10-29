using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using UserService.Data.Context;

namespace UserService.Data.Repositories;

public class EfRepository<T>(UserServiceDbContext context) : IRepository<T>
    where T : class
{
    protected UserServiceDbContext Context { get; } = context;

    public Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken token) =>
        Context.Set<T>().AsNoTracking().FirstOrDefaultAsync(predicate, token);

    public Task<T?> FindForUpdateAsync(Expression<Func<T, bool>> predicate, CancellationToken token) =>
        Context.Set<T>().FirstOrDefaultAsync(predicate, token);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken token) =>
        Context.Set<T>().AsNoTracking().AnyAsync(predicate, token);

    public async Task AddAsync(T entity, CancellationToken token) =>
        await Context.Set<T>().AddAsync(entity, token);
}
