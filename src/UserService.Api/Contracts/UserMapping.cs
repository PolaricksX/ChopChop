using UserService.Domain.Entities;

namespace UserService.Api.Contracts;

public static class UserMapping
{
    public static UserResponse ToResponse(this UserProfile profile) =>
        new(
            profile.Id,
            profile.Username,
            profile.Email,
            profile.DisplayName,
            profile.CreatedAt,
            profile.UpdatedAt,
            VersionToken.Encode(profile.RowVersion));
}

public static class VersionToken
{
    private const int RowVersionLength = 8;

    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static bool TryDecode(string? token, out byte[] rowVersion)
    {
        rowVersion = new byte[RowVersionLength];
        return !string.IsNullOrEmpty(token)
            && token.Length <= 24
            && Convert.TryFromBase64String(token, rowVersion, out var written)
            && written == RowVersionLength;
    }
}
