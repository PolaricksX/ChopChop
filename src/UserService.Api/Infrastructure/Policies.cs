namespace UserService.Api.Infrastructure;

public static class Policies
{
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
}

public static class ScopeClaims
{
    /// <summary>Reads space-delimited <c>scope</c> (and <c>scp</c>) claims.</summary>
    public static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope) =>
        user.Claims
            .Where(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
