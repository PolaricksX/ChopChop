using Microsoft.Extensions.DependencyInjection;
using UserService.Data.Repositories;
using UserService.Domain.Entities;
using UserService.Domain.Exceptions;
using UserService.IntegrationTests.Support;

namespace UserService.IntegrationTests;

/// <summary>Verifies database constraints and optimistic concurrency against real SQL Server.</summary>
public class PersistenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string Name() => "u" + Guid.NewGuid().ToString("N")[..12];

    private static UserProfile NewProfile(string? username = null, string? email = null) =>
        UserProfile.Create(Guid.NewGuid().ToString(), username ?? Name(), email ?? $"{Guid.NewGuid():N}@example.com", null, DateTimeOffset.UtcNow);

    private async Task SaveAsync(params UserProfile[] profiles)
    {
        using var scope = factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        foreach (var profile in profiles)
        {
            await uow.Repository<UserProfile>().AddAsync(profile, CancellationToken.None);
        }

        await uow.SaveChangesAsync();
    }

    [Fact]
    public async Task Unique_email_index_surfaces_as_a_duplicate_user_error()
    {
        var first = NewProfile(email: "dup@example.com");
        await SaveAsync(first);

        var ex = await Assert.ThrowsAsync<DuplicateUserException>(() => SaveAsync(NewProfile(email: "dup@example.com")));

        Assert.Equal("email", ex.Field);
    }

    [Fact]
    public async Task Unique_username_index_surfaces_as_a_duplicate_user_error()
    {
        var name = Name();
        await SaveAsync(NewProfile(username: name));

        var ex = await Assert.ThrowsAsync<DuplicateUserException>(() => SaveAsync(NewProfile(username: name.ToUpperInvariant())));

        Assert.Equal("username", ex.Field);
    }

    [Fact]
    public async Task Unique_subject_index_surfaces_as_a_duplicate_user_error()
    {
        var first = NewProfile();
        await SaveAsync(first);
        var sameSubject = UserProfile.Create(first.Subject, Name(), $"{Guid.NewGuid():N}@example.com", null, DateTimeOffset.UtcNow);

        var ex = await Assert.ThrowsAsync<DuplicateUserException>(() => SaveAsync(sameSubject));

        Assert.Equal("subject", ex.Field);
    }

    [Fact]
    public async Task Concurrent_updates_with_the_same_version_conflict()
    {
        var profile = NewProfile();
        await SaveAsync(profile);

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var uowA = scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var uowB = scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reposA = uowA.CustomRepository<IUserProfileRepository>();
        var reposB = uowB.CustomRepository<IUserProfileRepository>();

        var a = (await reposA.GetOwnedAsync(profile.Id, profile.Subject, true, CancellationToken.None))!;
        var b = (await reposB.GetOwnedAsync(profile.Id, profile.Subject, true, CancellationToken.None))!;
        a.Update(Name(), "a@example.com", null, DateTimeOffset.UtcNow);
        b.Update(Name(), "b@example.com", null, DateTimeOffset.UtcNow);

        await uowA.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => uowB.SaveChangesAsync());
    }

    [Fact]
    public async Task Idempotency_key_is_unique_per_subject()
    {
        var profile = NewProfile();
        await SaveAsync(profile);
        var key = Guid.NewGuid().ToString();

        async Task AddKeyAsync(string subject)
        {
            using var scope = factory.Services.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await uow.Repository<IdempotencyRecord>().AddAsync(
                IdempotencyRecord.Create(subject, key, "hash", profile.Id, DateTimeOffset.UtcNow), CancellationToken.None);
            await uow.SaveChangesAsync();
        }

        await AddKeyAsync("subject-1");
        await AddKeyAsync("subject-2");
        await Assert.ThrowsAsync<ConflictException>(() => AddKeyAsync("subject-1"));
    }
}
