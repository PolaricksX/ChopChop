using UserService.Domain.Services;

namespace UserService.Api.Contracts;

/// <summary>Boundary validation. Reuses domain rules so API and domain never disagree.</summary>
public static class UserRequestValidator
{
    public const int IdempotencyKeyMaxLength = 128;

    public static Dictionary<string, string[]> Validate(CreateUserRequest? request, string? idempotencyKey)
    {
        var errors = request is null
            ? new Dictionary<string, string[]> { ["body"] = ["A request body is required."] }
            : new Dictionary<string, string[]>(UserProfileRules.Validate(request.Username, request.Email, request.DisplayName));

        if (!IsValidIdempotencyKey(idempotencyKey))
        {
            errors["Idempotency-Key"] = [$"An Idempotency-Key header of 1-{IdempotencyKeyMaxLength} printable ASCII characters is required."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(UpdateUserRequest? request)
    {
        if (request is null)
        {
            return new Dictionary<string, string[]> { ["body"] = ["A request body is required."] };
        }

        var errors = new Dictionary<string, string[]>(UserProfileRules.Validate(request.Username, request.Email, request.DisplayName));
        if (!VersionToken.TryDecode(request.Version, out _))
        {
            errors["version"] = ["Version is required and must be the value last returned for this user."];
        }

        return errors;
    }

    private static bool IsValidIdempotencyKey(string? key) =>
        !string.IsNullOrEmpty(key)
        && key.Length <= IdempotencyKeyMaxLength
        && key.All(c => c is >= '!' and <= '~');
}
