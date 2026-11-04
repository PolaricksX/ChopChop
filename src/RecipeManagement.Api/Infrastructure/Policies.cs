using System.Security.Claims;

namespace RecipeManagement.Api.Infrastructure;

public static class Policies
{
    public const string RecipesRead = "recipes.read";
    public const string RecipesWrite = "recipes.write";
    public const string RecipesPublish = "recipes.publish";
}

public static class ScopeClaims
{
    /// <summary>Reads space-delimited <c>scope</c> and <c>scp</c> claims.</summary>
    public static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.Claims
            .Where(claim => claim.Type is "scope" or "scp")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
