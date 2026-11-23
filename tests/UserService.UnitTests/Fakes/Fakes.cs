using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using UserService.Data.Context;
using UserService.Data.Images;
using UserService.Data.Repositories;
using UserService.Data.Storage;
using UserService.Domain.Entities;
using UserService.Domain.Services;

namespace UserService.UnitTests.Fakes;

public class InMemoryRepository<T> : IRepository<T>
    where T : class
{
    public List<T> Items { get; } = [];

    public Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken token) =>
        Task.FromResult(Items.FirstOrDefault(predicate.Compile()));

    public Task<T?> FindForUpdateAsync(Expression<Func<T, bool>> predicate, CancellationToken token) => GetAsync(predicate, token);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken token) =>
        Task.FromResult(Items.Any(predicate.Compile()));

    public Task AddAsync(T entity, CancellationToken token)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }
}

public sealed class FakeUserProfileRepository : InMemoryRepository<UserProfile>, IUserProfileRepository
{
    public Task<UserProfile?> GetOwnedAsync(Guid id, string subject, bool forUpdate, CancellationToken token) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.Subject == subject && !x.IsDeleted));

    public Task<UserProfile?> GetBySubjectAsync(string subject, CancellationToken token) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Subject == subject && !x.IsDeleted));

    public void SetExpectedVersion(UserProfile profile, byte[] version)
    {
    }
}

public sealed class FakeRepositoryFactory : IRepositoryFactory
{
    public FakeUserProfileRepository Users { get; } = new();

    public InMemoryRepository<IdempotencyRecord> Keys { get; } = new();

    public IRepository<T> Create<T>()
        where T : class =>
        (IRepository<T>)(object)(typeof(T) == typeof(UserProfile) ? Users : Keys);

    public TRepository CreateCustom<TRepository>()
        where TRepository : class =>
        (TRepository)(object)Users;
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public static class TestDbContext
{
    public static UserServiceDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<UserServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

public sealed class FakeProfilePictureStorage : IProfilePictureStorage
{
    public Dictionary<string, byte[]> Saved { get; } = [];
    public List<string> Deleted { get; } = [];

    public Task<string> SaveAsync(byte[] bytes, ProfilePictureFormat format, CancellationToken cancellationToken)
    {
        var key = $"{Guid.NewGuid()}.{format.ToString().ToLowerInvariant()}";
        Saved[key] = bytes;
        return Task.FromResult(key);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Saved.Remove(key);
        Deleted.Add(key);
        return Task.CompletedTask;
    }
}

public sealed class FakeImageIntegrityChecker(bool isValid = true) : IImageIntegrityChecker
{
    public Task<bool> IsValidImageAsync(byte[] imageBytes, CancellationToken cancellationToken) =>
        Task.FromResult(isValid);
}
