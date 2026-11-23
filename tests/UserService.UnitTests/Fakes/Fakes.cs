using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    public bool FailDeletes { get; set; }

    public Task<string> SaveAsync(byte[] bytes, ProfilePictureFormat format, CancellationToken cancellationToken)
    {
        var key = $"{Guid.NewGuid()}.{format.ToString().ToLowerInvariant()}";
        Saved[key] = bytes;
        return Task.FromResult(key);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (FailDeletes)
        {
            return Task.FromException(new IOException("Simulated delete failure."));
        }

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

public sealed class FailingUnitOfWork(IUnitOfWork inner, Exception failure) : IUnitOfWork
{
    public IRepository<T> Repository<T>()
        where T : class => inner.Repository<T>();

    public TRepository CustomRepository<TRepository>()
        where TRepository : class => inner.CustomRepository<TRepository>();

    public Task<int> SaveChangesAsync(CancellationToken token = default) => Task.FromException<int>(failure);
}

public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
