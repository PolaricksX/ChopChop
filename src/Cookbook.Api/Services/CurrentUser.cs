using System.Security.Claims;

namespace Cookbook.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var subject = httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
                throw new UnauthorizedAccessException("A valid subject claim is required.");

            return userId;
        }
    }
}
