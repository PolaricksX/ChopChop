namespace UserService.Domain.Exceptions;

/// <summary>Raised on a stale version (optimistic concurrency) or an idempotency key reused with a different payload.</summary>
public sealed class ConflictException(string message) : Exception(message);
