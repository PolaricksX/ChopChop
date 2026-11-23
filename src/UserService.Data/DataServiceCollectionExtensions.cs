using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserService.Data.Images;
using UserService.Data.Context;
using UserService.Data.Repositories;
using UserService.Data.Storage;
using UserService.Domain.Entities;

namespace UserService.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers the DbContext, factory, provider, Unit of Work, and specific repositories in one request scope.</summary>
    public static IServiceCollection AddUserServiceData(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<UserServiceDbContext>((sp, options) =>
            options.UseSqlServer(connectionString(sp), sql => sql.EnableRetryOnFailure()));

        services.AddOptions<ProfilePictureStorageOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.RootPath = configuration[$"{ProfilePictureStorageOptions.SectionName}:RootPath"] ?? string.Empty)
            .Validate(ProfilePictureStorageOptions.IsValid, "ProfilePictures:RootPath is required and must be an absolute path.")
            .ValidateOnStart();

        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IRepository<UserProfile>>(sp => sp.GetRequiredService<IUserProfileRepository>());
        services.AddScoped<IImageIntegrityChecker, SkiaSharpImageIntegrityChecker>();
        services.AddScoped<IProfilePictureStorage, LocalProfilePictureStorage>();
        return services;
    }
}
