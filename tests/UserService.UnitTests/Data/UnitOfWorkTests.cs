using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserService.Data;
using UserService.Data.Context;
using UserService.Data.Repositories;
using UserService.Domain.Entities;
using UserService.UnitTests.Fakes;

namespace UserService.UnitTests.Data;

public class UnitOfWorkTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddUserServiceData(_ => "Server=unused;Database=unused;TrustServerCertificate=True");
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Repositories_from_the_unit_of_work_use_the_scoped_db_context()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var context = scope.ServiceProvider.GetRequiredService<UserServiceDbContext>();
        var record = IdempotencyRecord.Create("sub", "k", "h", Guid.NewGuid(), DateTimeOffset.UtcNow);

        await uow.Repository<IdempotencyRecord>().AddAsync(record, CancellationToken.None);

        Assert.Equal(EntityState.Added, context.Entry(record).State);
    }

    [Fact]
    public void Specific_repository_is_registered_for_both_interfaces()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.Same(
            scope.ServiceProvider.GetRequiredService<IUserProfileRepository>(),
            scope.ServiceProvider.GetRequiredService<IRepository<UserProfile>>());
    }

    [Fact]
    public void Unit_of_work_returns_the_specific_repository_for_its_aggregate()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.IsType<UserProfileRepository>(uow.Repository<UserProfile>());
        Assert.IsType<EfRepository<IdempotencyRecord>>(uow.Repository<IdempotencyRecord>());
    }

    [Fact]
    public void Provider_caches_repositories_per_scope()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.Same(uow.Repository<IdempotencyRecord>(), uow.Repository<IdempotencyRecord>());
        Assert.Same(uow.CustomRepository<IUserProfileRepository>(), uow.CustomRepository<IUserProfileRepository>());
    }

    [Fact]
    public async Task Repositories_stage_only_and_one_commit_persists_across_repositories()
    {
        await using var context = TestDbContext.CreateInMemory();
        var factory = new RepositoryFactory(new ServiceCollection().BuildServiceProvider(), context);
        var uow = new UnitOfWork(context, new RepositoryProvider(factory));
        var now = DateTimeOffset.UtcNow;
        var profile = UserProfile.Create("sub", "alice", "a@example.com", null, now);

        await uow.Repository<UserProfile>().AddAsync(profile, CancellationToken.None);
        await uow.Repository<IdempotencyRecord>().AddAsync(IdempotencyRecord.Create("sub", "k", "h", profile.Id, now), CancellationToken.None);

        Assert.Equal(0, await context.Users.AsNoTracking().CountAsync());
        Assert.Equal(0, await context.IdempotencyRecords.AsNoTracking().CountAsync());

        Assert.Equal(2, await uow.SaveChangesAsync());

        Assert.Equal(1, await context.Users.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.IdempotencyRecords.AsNoTracking().CountAsync());
    }
}
