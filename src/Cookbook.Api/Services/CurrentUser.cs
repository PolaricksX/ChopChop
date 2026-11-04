using ChopChop.Shared;

namespace Cookbook.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.TryGetUserId(out var userId) != true)
                throw new UnauthorizedAccessException("A valid subject and issuer claim are required.");

            return userId;
        }
    }
}
