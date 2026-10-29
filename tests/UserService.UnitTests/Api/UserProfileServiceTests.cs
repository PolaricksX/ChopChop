using Microsoft.Extensions.Logging.Abstractions;
using UserService.Api.Contracts;
using UserService.Api.Services;
using UserService.Data.Repositories;
using UserService.Domain.Exceptions;
using UserService.UnitTests.Fakes;

namespace UserService.UnitTests.Api;

public class UserProfileServiceTests
{
    private const string AnyVersion = "AAAAAAAAAAA=";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly FakeRepositoryFactory factory = new();
    private readonly UserProfileService service;

    public UserProfileServiceTests()
    {
        var unitOfWork = new UnitOfWork(TestDbContext.CreateInMemory(), new RepositoryProvider(factory));
        service = new UserProfileService(unitOfWork, new FixedTimeProvider(Now), NullLogger<UserProfileService>.Instance);
    }

    private static CreateUserRequest Request(string username = "alice", string email = "alice@example.com") =>
        new(username, email, "Alice");

    [Fact]
    public async Task Create_stores_profile_for_caller_and_records_idempotency_key()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        Assert.Equal("sub-1", profile.Subject);
        Assert.Single(factory.Users.Items);
        Assert.Single(factory.Keys.Items);
    }

    [Fact]
    public async Task Create_with_same_key_and_payload_returns_the_original_without_duplicating()
    {
        var first = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);
        var second = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(factory.Users.Items);
    }

    [Fact]
    public async Task Create_with_same_key_and_different_payload_conflicts()
    {
        await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync("sub-1", "key-1", Request(username: "bob"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_twice_for_one_account_with_new_key_conflicts()
    {
        await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateUserException>(() =>
            service.CreateAsync("sub-1", "key-2", Request(username: "bob", email: "bob@example.com"), CancellationToken.None));
        Assert.Equal("subject", ex.Field);
    }

    [Fact]
    public async Task Create_rejects_duplicate_email_case_insensitively()
    {
        await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateUserException>(() =>
            service.CreateAsync("sub-2", "key-2", Request(username: "bob", email: "ALICE@example.com"), CancellationToken.None));
        Assert.Equal("email", ex.Field);
    }

    [Fact]
    public async Task Create_rejects_duplicate_username_case_insensitively()
    {
        await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateUserException>(() =>
            service.CreateAsync("sub-2", "key-2", Request(username: "ALICE", email: "bob@example.com"), CancellationToken.None));
        Assert.Equal("username", ex.Field);
    }

    [Fact]
    public async Task Create_with_invalid_input_throws_validation_error() =>
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CreateAsync("sub-1", "key-1", Request(username: "x"), CancellationToken.None));

    [Fact]
    public async Task Get_returns_own_profile_and_hides_other_users_profiles()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        Assert.Equal(profile.Id, (await service.GetAsync("sub-1", profile.Id, CancellationToken.None)).Id);
        await Assert.ThrowsAsync<UserNotFoundException>(() => service.GetAsync("sub-2", profile.Id, CancellationToken.None));
        await Assert.ThrowsAsync<UserNotFoundException>(() => service.GetAsync("sub-1", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Update_changes_own_profile()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        var updated = await service.UpdateAsync(
            "sub-1", profile.Id, new UpdateUserRequest("alice2", "alice2@example.com", null, AnyVersion), CancellationToken.None);

        Assert.Equal("alice2", updated.Username);
        Assert.Equal("alice2@example.com", updated.Email);
    }

    [Fact]
    public async Task Update_of_another_users_profile_is_not_found()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await Assert.ThrowsAsync<UserNotFoundException>(() => service.UpdateAsync(
            "sub-2", profile.Id, new UpdateUserRequest("x-user", "x@example.com", null, AnyVersion), CancellationToken.None));
    }

    [Fact]
    public async Task Update_to_another_users_email_conflicts()
    {
        await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);
        var bob = await service.CreateAsync("sub-2", "key-2", Request("bob", "bob@example.com"), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateUserException>(() => service.UpdateAsync(
            "sub-2", bob.Id, new UpdateUserRequest("bob", "alice@example.com", null, AnyVersion), CancellationToken.None));
        Assert.Equal("email", ex.Field);
    }

    [Fact]
    public async Task Update_keeping_own_email_and_username_is_allowed()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        var updated = await service.UpdateAsync(
            "sub-1", profile.Id, new UpdateUserRequest("alice", "alice@example.com", "New Name", AnyVersion), CancellationToken.None);

        Assert.Equal("New Name", updated.DisplayName);
    }

    [Fact]
    public async Task Delete_soft_deletes_and_then_hides_the_profile()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await service.DeleteAsync("sub-1", profile.Id, CancellationToken.None);

        Assert.True(factory.Users.Items.Single().IsDeleted);
        await Assert.ThrowsAsync<UserNotFoundException>(() => service.GetAsync("sub-1", profile.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_of_another_users_profile_is_not_found_and_leaves_it_intact()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await Assert.ThrowsAsync<UserNotFoundException>(() => service.DeleteAsync("sub-2", profile.Id, CancellationToken.None));
        Assert.False(factory.Users.Items.Single().IsDeleted);
    }
}
