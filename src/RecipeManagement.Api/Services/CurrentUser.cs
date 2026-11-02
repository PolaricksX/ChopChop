using System.Security.Claims;

namespace RecipeManagement.Api.Services;

public interface ICurrentUser { Guid UserId { get; } }

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("sub"), out var id) && id != Guid.Empty
        ? id : throw new UnauthorizedAccessException("A valid subject claim is required.");
}
