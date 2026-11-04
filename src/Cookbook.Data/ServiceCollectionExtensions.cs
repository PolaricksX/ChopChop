using Cookbook.Data.Context;
using Cookbook.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cookbook.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCookbookData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CookbookDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("ChopChop")
                ?? throw new InvalidOperationException("ConnectionStrings:ChopChop is required.");
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<CookbookDbContext>());
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICookbookQueries, CookbookQueries>();
        return services;
    }
}
