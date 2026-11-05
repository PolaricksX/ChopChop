using ChopChop.Shared;

namespace RecipeManagement.Api.Services;

public interface ICurrentUser { Guid UserId { get; } }

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId => accessor.HttpContext?.User.TryGetUserId(out var id) == true
        ? id : throw new UnauthorizedAccessException("A valid subject and issuer claim are required.");
}
