namespace Cookbook.Data.Repositories;

public sealed class RepositoryProvider(IRepositoryFactory factory) : IRepositoryProvider
{
    private readonly Dictionary<Type, object> cache = [];

    public IRepository<T> GetRepository<T>() where T : class =>
        GetOrCreate(() => factory.Create<T>());

    public TRepository GetCustomRepository<TRepository>() where TRepository : class =>
        GetOrCreate(() => factory.CreateCustom<TRepository>());

    private TService GetOrCreate<TService>(Func<TService> create) where TService : class
    {
        if (!cache.TryGetValue(typeof(TService), out var value))
            cache[typeof(TService)] = value = create();

        return (TService)value;
    }
}
