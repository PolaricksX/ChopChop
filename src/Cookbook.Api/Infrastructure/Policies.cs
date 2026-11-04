using System.Security.Claims;

namespace Cookbook.Api.Infrastructure;

public static class Policies
{
    public const string CookbookRead = "cookbook.read";
    public const string CookbookWrite = "cookbook.write";
}

public static class ScopeClaims
{
    public static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.Claims
            .Where(claim => claim.Type is "scope" or "scp")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
