namespace Cookbook.Api.Services;

public interface ICurrentUser
{
    Guid UserId { get; }
}
