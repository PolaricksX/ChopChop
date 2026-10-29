using System.Security.Cryptography;
using System.Text;
using UserService.Api.Contracts;
using UserService.Data.Repositories;
using UserService.Domain.Entities;
using UserService.Domain.Exceptions;

namespace UserService.Api.Services;

/// <summary>Use-case orchestration for profiles. Every private lookup is filtered by the caller's subject.</summary>
public sealed class UserProfileService(IUnitOfWork unitOfWork, TimeProvider clock, ILogger<UserProfileService> logger)
{
    public async Task<UserProfile> CreateAsync(
        string subject,
        string idempotencyKey,
        CreateUserRequest request,
        CancellationToken token)
    {
        var users = unitOfWork.CustomRepository<IUserProfileRepository>();
        var keys = unitOfWork.Repository<IdempotencyRecord>();
        var requestHash = Fingerprint(request);

        var previous = await keys.GetAsync(x => x.Subject == subject && x.Key == idempotencyKey, token);
        if (previous is not null)
        {
            if (previous.RequestHash != requestHash)
            {
                throw new ConflictException("The Idempotency-Key was already used with a different request.");
            }

            return await users.GetOwnedAsync(previous.UserProfileId, subject, forUpdate: false, token)
                ?? throw new ConflictException("The profile created by this Idempotency-Key no longer exists.");
        }

        var now = clock.GetUtcNow();
        var profile = UserProfile.Create(subject, request.Username!, request.Email!, request.DisplayName, now);

        if (await users.AnyAsync(x => x.Subject == subject, token))
        {
            throw new DuplicateUserException("subject", "A profile already exists for this account.");
        }

        await EnsureUniqueAsync(users, profile, token);

        await users.AddAsync(profile, token);
        await keys.AddAsync(IdempotencyRecord.Create(subject, idempotencyKey, requestHash, profile.Id, now), token);
        await unitOfWork.SaveChangesAsync(token);

        logger.LogInformation("User profile {UserId} created", profile.Id);
        return await users.GetOwnedAsync(profile.Id, subject, forUpdate: false, token) ?? profile;
    }

    public async Task<UserProfile> GetAsync(string subject, Guid id, CancellationToken token) =>
        await unitOfWork.CustomRepository<IUserProfileRepository>().GetOwnedAsync(id, subject, forUpdate: false, token)
        ?? throw new UserNotFoundException();

    public async Task<UserProfile> UpdateAsync(string subject, Guid id, UpdateUserRequest request, CancellationToken token)
    {
        var users = unitOfWork.CustomRepository<IUserProfileRepository>();
        var profile = await users.GetOwnedAsync(id, subject, forUpdate: true, token) ?? throw new UserNotFoundException();

        profile.Update(request.Username!, request.Email!, request.DisplayName, clock.GetUtcNow());
        await EnsureUniqueAsync(users, profile, token);

        VersionToken.TryDecode(request.Version, out var expected);
        users.SetExpectedVersion(profile, expected);
        await unitOfWork.SaveChangesAsync(token);

        logger.LogInformation("User profile {UserId} updated", profile.Id);
        return await users.GetOwnedAsync(id, subject, forUpdate: false, token) ?? throw new UserNotFoundException();
    }

    public async Task DeleteAsync(string subject, Guid id, CancellationToken token)
    {
        var users = unitOfWork.CustomRepository<IUserProfileRepository>();
        var profile = await users.GetOwnedAsync(id, subject, forUpdate: true, token) ?? throw new UserNotFoundException();

        profile.Delete(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(token);

        logger.LogInformation("User profile {UserId} deleted", id);
    }

    // Friendly pre-check; the unique indexes remain the source of truth under concurrency.
    private static async Task EnsureUniqueAsync(IUserProfileRepository users, UserProfile profile, CancellationToken token)
    {
        var id = profile.Id;
        var email = profile.Email;
        var username = profile.NormalizedUsername;

        if (await users.AnyAsync(x => x.Id != id && x.Email == email, token))
        {
            throw new DuplicateUserException("email", "A user with this email already exists.");
        }

        if (await users.AnyAsync(x => x.Id != id && x.NormalizedUsername == username, token))
        {
            throw new DuplicateUserException("username", "A user with this username already exists.");
        }
    }

    // A fingerprint to detect key reuse with a different payload; not used for security.
    private static string Fingerprint(CreateUserRequest request)
    {
        var payload = string.Join('\0', request.Username?.Trim(), request.Email?.Trim(), request.DisplayName?.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
