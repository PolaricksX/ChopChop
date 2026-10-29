using System.Net.Mail;
using System.Text.RegularExpressions;

namespace UserService.Domain.Services;

/// <summary>Pure validation and normalization rules for profile fields.</summary>
public static partial class UserProfileRules
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 30;
    public const int EmailMaxLength = 254;
    public const int DisplayNameMaxLength = 100;
    public const int SubjectMaxLength = 256;

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex UsernamePattern();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static IReadOnlyDictionary<string, string[]> Validate(string? username, string? email, string? displayName)
    {
        var errors = new Dictionary<string, string[]>();

        var trimmedUsername = username?.Trim();
        if (string.IsNullOrEmpty(trimmedUsername))
        {
            errors["username"] = ["Username is required."];
        }
        else if (trimmedUsername.Length is < UsernameMinLength or > UsernameMaxLength)
        {
            errors["username"] = [$"Username must be {UsernameMinLength}-{UsernameMaxLength} characters."];
        }
        else if (!UsernamePattern().IsMatch(trimmedUsername))
        {
            errors["username"] = ["Username may contain letters, digits, '.', '_' and '-', and must start with a letter or digit."];
        }

        var trimmedEmail = email?.Trim();
        if (string.IsNullOrEmpty(trimmedEmail))
        {
            errors["email"] = ["Email is required."];
        }
        else if (!IsValidEmail(trimmedEmail))
        {
            errors["email"] = ["Email must be a valid address of at most 254 characters."];
        }

        if (displayName is not null)
        {
            var trimmedDisplayName = displayName.Trim();
            if (trimmedDisplayName.Length > DisplayNameMaxLength)
            {
                errors["displayName"] = [$"Display name must be at most {DisplayNameMaxLength} characters."];
            }
            else if (trimmedDisplayName.Any(char.IsControl))
            {
                errors["displayName"] = ["Display name must not contain control characters."];
            }
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Length > EmailMaxLength || email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            return false;
        }

        // Require a dotted domain; MailAddress accepts "user@localhost".
        return parsed.Host.Contains('.', StringComparison.Ordinal) && !parsed.Host.EndsWith('.');
    }
}
