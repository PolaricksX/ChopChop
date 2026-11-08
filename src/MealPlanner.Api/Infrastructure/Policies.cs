namespace MealPlanner.Api.Infrastructure;

public static class Policies
{
    public const string MealPlansRead = "mealplans.read";
    public const string MealPlansWrite = "mealplans.write";
}

public static class ScopeClaims
{
    public static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope) =>
        user.HasClaim(claim => claim.Type == "sub" && !string.IsNullOrWhiteSpace(claim.Value))
        && user.Claims
            .Where(claim => claim.Type is "scope" or "scp" or "permissions")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
