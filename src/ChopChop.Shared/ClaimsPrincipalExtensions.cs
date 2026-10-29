using System.Security.Claims;

namespace ChopChop.Shared;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out userId) && userId != Guid.Empty;
}
