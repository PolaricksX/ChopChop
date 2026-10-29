using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserService.Data.Context;
using UserService.Data.Repositories;
using UserService.Domain.Entities;

namespace UserService.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers the DbContext, factory, provider, Unit of Work, and specific repositories in one request scope.</summary>
    public static IServiceCollection AddUserServiceData(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<UserServiceDbContext>((sp, options) =>
            options.UseSqlServer(connectionString(sp), sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IRepository<UserProfile>>(sp => sp.GetRequiredService<IUserProfileRepository>());
        return services;
    }
}
