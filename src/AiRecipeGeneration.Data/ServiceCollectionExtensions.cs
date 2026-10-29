using AiRecipeGeneration.Data.Context;
using AiRecipeGeneration.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiRecipeGeneration.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiRecipeData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AiRecipeDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("ChopChop")
                ?? throw new InvalidOperationException("ConnectionStrings:ChopChop is required.");
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<AiRecipeDbContext>());
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IGenerationQueries, GenerationQueries>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxQueries, OutboxQueries>();
        return services;
    }
}
