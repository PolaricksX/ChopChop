namespace Equipment.Api.Infrastructure;

public static class EquipmentPolicies
{
    public const string Read = "equipment.read";
    public const string Write = "equipment.write";
}

public static class ScopeClaims
{
    public static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope) =>
        user.HasClaim(claim =>
            claim.Type == "sub" && !string.IsNullOrWhiteSpace(claim.Value))
        && user.Claims
            .Where(claim => claim.Type is "scope" or "scp")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
