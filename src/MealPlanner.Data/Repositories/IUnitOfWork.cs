namespace MealPlanner.Data.Repositories;

public interface IUnitOfWork
{
    IRepository<T> Repository<T>()
        where T : class;

    TRepository CustomRepository<TRepository>()
        where TRepository : class;

    Task<int> SaveChangesAsync(CancellationToken token = default);
}
