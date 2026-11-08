using System.Linq.Expressions;
using Equipment.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Equipment.Data.Repositories;

public class EfRepository<T>(EquipmentDbContext context) : IRepository<T>
    where T : class
{
    public Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken token) =>
        context.Set<T>().FirstOrDefaultAsync(predicate, token);

    public Task<T?> FindForUpdateAsync(Expression<Func<T, bool>> predicate, CancellationToken token) =>
        context.Set<T>().FirstOrDefaultAsync(predicate, token);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken token) =>
        context.Set<T>().AnyAsync(predicate, token);

    public async Task AddAsync(T entity, CancellationToken token) =>
        await context.Set<T>().AddAsync(entity, token);
}
