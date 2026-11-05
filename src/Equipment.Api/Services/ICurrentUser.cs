namespace Equipment.Api.Services;

public interface ICurrentUser
{
    string Id { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string Id =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("No authenticated subject is available.");
}
