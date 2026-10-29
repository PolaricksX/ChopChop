using Microsoft.Extensions.DependencyInjection;
using UserService.Data.Context;

namespace UserService.Data.Repositories;

public interface IRepositoryFactory
{
    IRepository<T> Create<T>()
        where T : class;

    TRepository CreateCustom<TRepository>()
        where TRepository : class;
}

public sealed class RepositoryFactory(IServiceProvider services, UserServiceDbContext context) : IRepositoryFactory
{
    public IRepository<T> Create<T>()
        where T : class =>
        services.GetService<IRepository<T>>() ?? new EfRepository<T>(context);

    public TRepository CreateCustom<TRepository>()
        where TRepository : class =>
        services.GetRequiredService<TRepository>();
}

public interface IRepositoryProvider
{
    IRepository<T> GetRepository<T>()
        where T : class;

    TRepository GetCustomRepository<TRepository>()
        where TRepository : class;
}

/// <summary>Caches one repository per type for the lifetime of the request scope.</summary>
public sealed class RepositoryProvider(IRepositoryFactory factory) : IRepositoryProvider
{
    private readonly Dictionary<Type, object> cache = [];

    public IRepository<T> GetRepository<T>()
        where T : class =>
        GetOrCreate(() => factory.Create<T>());

    public TRepository GetCustomRepository<TRepository>()
        where TRepository : class =>
        GetOrCreate(() => factory.CreateCustom<TRepository>());

    private TService GetOrCreate<TService>(Func<TService> create)
        where TService : class
    {
        if (!cache.TryGetValue(typeof(TService), out var repository))
        {
            cache[typeof(TService)] = repository = create();
        }

        return (TService)repository;
    }
}
