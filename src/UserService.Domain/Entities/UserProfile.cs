using UserService.Domain.Exceptions;
using UserService.Domain.Services;

namespace UserService.Domain.Entities;

/// <summary>The profile record, keyed by the identity provider's token <c>sub</c> claim.</summary>
public sealed class UserProfile
{
    private UserProfile()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Identity-provider subject. Server-owned; never bound from request bodies.</summary>
    public string Subject { get; private set; } = string.Empty;

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    /// <summary>Stored lowercase so uniqueness is case-insensitive regardless of collation.</summary>
    public string Email { get; private set; } = string.Empty;

    public string? DisplayName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted => DeletedAt is not null;

    public static UserProfile Create(string subject, string username, string email, string? displayName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > UserProfileRules.SubjectMaxLength)
        {
            throw new DomainValidationException(new Dictionary<string, string[]> { ["subject"] = ["Subject is invalid."] });
        }

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            CreatedAt = now,
        };
        profile.Apply(username, email, displayName, now);
        return profile;
    }

    public void Update(string username, string email, string? displayName, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            throw new UserNotFoundException();
        }

        Apply(username, email, displayName, now);
    }

    /// <summary>Soft delete. Calling it again keeps the original deletion time.</summary>
    public void Delete(DateTimeOffset now)
    {
        DeletedAt ??= now;
        UpdatedAt = now;
    }

    private void Apply(string username, string email, string? displayName, DateTimeOffset now)
    {
        var errors = UserProfileRules.Validate(username, email, displayName);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        Username = username.Trim();
        NormalizedUsername = UserProfileRules.NormalizeUsername(username);
        Email = UserProfileRules.NormalizeEmail(email);
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        UpdatedAt = now;
    }
}
