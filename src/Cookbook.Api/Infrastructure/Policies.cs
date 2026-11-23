using System.Security.Claims;
using ChopChop.Shared;

namespace Cookbook.Api.Infrastructure;

public static class Policies
{
    public const string CookbookRead = "cookbook.read";
    public const string CookbookWrite = "cookbook.write";
}

public static class ScopeClaims
{
    // The scope alone is not enough: ownership needs a usable subject, so a token without one is forbidden here rather than failing later in ICurrentUser.
    public static bool CanUse(ClaimsPrincipal user, string scope) =>
        user.TryGetUserId(out _) && HasScope(user, scope);

    public static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.Claims
            .Where(claim => claim.Type is "scope" or "scp" or "permissions")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
