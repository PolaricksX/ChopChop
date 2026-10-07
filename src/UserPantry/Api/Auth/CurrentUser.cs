using System.Security.Claims;
using UserPantry.Application.Abstractions;

namespace UserPantry.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("The authenticated user identifier is unavailable.");

            return Guid.Parse(value);
        }
    }
}
