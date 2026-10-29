namespace UserService.Domain.Exceptions;

/// <summary>Raised when input breaks a domain invariant. Carries per-field messages.</summary>
public sealed class DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
