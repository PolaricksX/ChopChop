namespace AiRecipeGeneration.Data;

public interface IRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken cancellationToken);
    void Update(T entity);
    void Remove(T entity);
}

public interface IRepositoryProvider
{
    IRepository<T> Create<T>() where T : class;
}

public interface IRepositoryFactory
{
    IRepository<T> Create<T>() where T : class;
    T CreateCustom<T>() where T : class;
}

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class;
    T CustomRepository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
