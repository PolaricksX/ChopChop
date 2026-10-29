namespace UserService.Api.Contracts;

public sealed record CreateUserRequest(string? Username, string? Email, string? DisplayName);

/// <param name="Version">The <c>version</c> value last returned for this user; a stale value yields 409.</param>
public sealed record UpdateUserRequest(string? Username, string? Email, string? DisplayName, string? Version);

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string? DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Version);
