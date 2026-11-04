using InteractiveReview.Data.Context;
using InteractiveReview.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InteractiveReview.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInteractiveReviewData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<InteractiveReviewDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("ChopChop")
                ?? throw new InvalidOperationException("ConnectionStrings:ChopChop is required.");
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<InteractiveReviewDbContext>());
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReviewQueries, ReviewQueries>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxQueries, OutboxQueries>();
        return services;
    }
}
