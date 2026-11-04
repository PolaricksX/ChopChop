namespace Cookbook.Domain.Exceptions;

public class CookbookDomainException(string message) : InvalidOperationException(message);

public sealed class CookbookValidationException(string message) : CookbookDomainException(message);

public sealed class CookbookNotFoundException(string message) : CookbookDomainException(message);
