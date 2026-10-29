using System.Security.Claims;

namespace UserService.Api.Services;

/// <summary>The caller, derived only from validated token claims.</summary>
public interface ICurrentUser
{
    string Id { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string Id =>
        accessor.HttpContext?.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("No authenticated subject is available.");
}
