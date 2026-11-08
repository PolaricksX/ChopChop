using Equipment.Data.Context;
using Equipment.Data.Repositories;
using Equipment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipment.Data.Persistence;

namespace Equipment.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddEquipmentData(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "An Equipment database connection string is required.",
                nameof(connectionString));
        }

        services.AddDbContext<EquipmentDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IRepository<EquipmentItem>>(
            provider => provider.GetRequiredService<IEquipmentRepository>());
        services.AddScoped<IEquipmentOutboxWriter, EquipmentOutboxWriter>();
        services.AddScoped<IEquipmentOutboxQueries, EquipmentOutboxQueries>();

        return services;
    }
}
