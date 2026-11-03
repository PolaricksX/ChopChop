using Microsoft.Extensions.DependencyInjection;
using Cookbook.Data.Context;

namespace Cookbook.Data.Repositories;

public sealed class RepositoryFactory(IServiceProvider services, CookbookDbContext context) : IRepositoryFactory
{
    public IRepository<T> Create<T>() where T : class =>
        services.GetService<IRepository<T>>() ?? new EfRepository<T>(context);

    public TRepository CreateCustom<TRepository>() where TRepository : class =>
        services.GetRequiredService<TRepository>();
}
