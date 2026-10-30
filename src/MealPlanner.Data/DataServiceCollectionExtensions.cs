using MealPlanner.Data.Context;
using MealPlanner.Data.Events;
using MealPlanner.Data.Repositories;
using MealPlanner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MealPlanner.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddMealPlannerData(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<MealPlannerDbContext>((provider, options) =>
            options.UseSqlServer(connectionString(provider), sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMealPlanRepository, MealPlanRepository>();
        services.AddScoped<IRepository<MealPlan>>(provider => provider.GetRequiredService<IMealPlanRepository>());
        services.AddScoped<IEventBus, OutboxEventBus>();
        services.AddHostedService<RabbitMqOutboxPublisher>();
        return services;
    }
}
