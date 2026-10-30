namespace MealPlanner.Domain.Exceptions;

public sealed class DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more domain rules were violated.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
