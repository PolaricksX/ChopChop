using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ChopChop.Shared;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue("sub");
        if (Guid.TryParse(subject, out userId))
        {
            return userId != Guid.Empty;
        }

        var issuer = principal.FindFirstValue("iss");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer))
        {
            userId = Guid.Empty;
            return false;
        }

        var identity = Encoding.UTF8.GetBytes($"{issuer}\0{subject}");
        var hash = SHA256.HashData(identity);
        userId = new Guid(hash.AsSpan(0, 16));
        return userId != Guid.Empty;
    }

    public static bool HasScope(this ClaimsPrincipal principal, string requiredScope) =>
        principal.Claims
            .Where(claim => claim.Type is "scope" or "scp" or "permissions")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);
}
