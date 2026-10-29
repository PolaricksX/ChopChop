namespace UserService.Domain.Exceptions;

/// <summary>Raised when a profile does not exist or is not owned by the caller.</summary>
public sealed class UserNotFoundException() : Exception("The requested user was not found.");
