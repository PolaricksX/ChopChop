using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeManagement.Data.Context;
using RecipeManagement.Data.Repositories;

namespace RecipeManagement.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRecipeManagementData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RecipeManagementDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("ChopChop") ?? throw new InvalidOperationException("ConnectionStrings:ChopChop is required.")));
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<RecipeManagementDbContext>());
        services.AddScoped<IRepositoryProvider, RepositoryProvider>(); services.AddScoped<IRepositoryFactory, RepositoryFactory>(); services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRecipeQueries, RecipeQueries>(); services.AddScoped<IIdempotencyQueries, IdempotencyQueries>();
        return services;
    }
}
