using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UserService.Api.Contracts;
using UserService.Api.Services;
using UserService.Data.Repositories;
using UserService.Domain.Exceptions;
using UserService.Domain.Services;
using UserService.UnitTests.Fakes;

namespace UserService.UnitTests.Api;

public class UserProfileServiceTests
{
    private const string AnyVersion = "AAAAAAAAAAA=";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly FakeRepositoryFactory factory = new();
    private readonly FakeProfilePictureStorage storage = new();
    private readonly UnitOfWork unitOfWork;
    private readonly UserProfileService service;

    public UserProfileServiceTests()
    {
        unitOfWork = new UnitOfWork(TestDbContext.CreateInMemory(), new RepositoryProvider(factory));
        service = CreateService(new FakeImageIntegrityChecker());
    }

    private UserProfileService CreateService(FakeImageIntegrityChecker checker) =>
        new(unitOfWork, storage, checker, new FixedTimeProvider(Now), NullLogger<UserProfileService>.Instance);

    private static byte[] Png(int size = 64)
    {
        var bytes = new byte[size];
        PngSignature.CopyTo(bytes, 0);
        return bytes;
    }

    private static IFormFile Upload(byte[] bytes, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "picture")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

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

    [Fact]
    public async Task ReplacePicture_with_valid_png_saves_it_and_sets_the_key()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await service.ReplaceProfilePictureAsync("sub-1", profile.Id, Upload(Png(), "image/png"), CancellationToken.None);

        var savedKey = Assert.Single(storage.Saved).Key;
        Assert.Equal(savedKey, factory.Users.Items.Single().ProfilePictureKey);
    }

    [Fact]
    public async Task ReplacePicture_twice_deletes_the_old_picture()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await service.ReplaceProfilePictureAsync("sub-1", profile.Id, Upload(Png(), "image/png"), CancellationToken.None);
        var firstKey = factory.Users.Items.Single().ProfilePictureKey!;
        await service.ReplaceProfilePictureAsync("sub-1", profile.Id, Upload(Png(), "image/png"), CancellationToken.None);

        Assert.Contains(firstKey, storage.Deleted);
        Assert.Single(storage.Saved);
        Assert.NotEqual(firstKey, factory.Users.Items.Single().ProfilePictureKey);
    }

    [Fact]
    public async Task ReplacePicture_with_corrupted_image_is_rejected_and_nothing_is_saved()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);
        var rejectingService = CreateService(new FakeImageIntegrityChecker(isValid: false));

        await Assert.ThrowsAsync<ProfilePictureBadRequestException>(() =>
            rejectingService.ReplaceProfilePictureAsync("sub-1", profile.Id, Upload(Png(), "image/png"), CancellationToken.None));
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task ReplacePicture_with_empty_file_is_rejected()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await Assert.ThrowsAsync<ProfilePictureBadRequestException>(() =>
            service.ReplaceProfilePictureAsync("sub-1", profile.Id, Upload([], "image/png"), CancellationToken.None));
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task ReplacePicture_over_the_size_limit_is_rejected()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);
        var tooLarge = Png(ProfilePictureRules.MaxFileSizeBytes + 1);

        await Assert.ThrowsAsync<ProfilePicturePayloadTooLargeException>(() =>
            service.ReplaceProfilePictureAsync("sub-1", profile.Id, Upload(tooLarge, "image/png"), CancellationToken.None));
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task ReplacePicture_with_unsupported_format_is_rejected()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);
        byte[] gif = [.. "GIF89a"u8, 0, 0, 0, 0];

        await Assert.ThrowsAsync<ProfilePictureUnsupportedMediaTypeException>(() =>
            service.ReplaceProfilePictureAsync("sub-1", profile.Id, Upload(gif, "image/gif"), CancellationToken.None));
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task ReplacePicture_with_content_type_not_matching_the_file_is_rejected()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await Assert.ThrowsAsync<ProfilePictureUnsupportedMediaTypeException>(() =>
            service.ReplaceProfilePictureAsync("sub-1", profile.Id, Upload(Png(), "image/jpeg"), CancellationToken.None));
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task ReplacePicture_of_another_users_profile_is_not_found()
    {
        var profile = await service.CreateAsync("sub-1", "key-1", Request(), CancellationToken.None);

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            service.ReplaceProfilePictureAsync("sub-2", profile.Id, Upload(Png(), "image/png"), CancellationToken.None));
        Assert.Empty(storage.Saved);
    }
}
