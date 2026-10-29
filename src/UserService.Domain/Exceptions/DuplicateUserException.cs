namespace UserService.Domain.Exceptions;

/// <summary>Raised when a uniqueness rule is broken (username, email, or one profile per account).</summary>
public sealed class DuplicateUserException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
