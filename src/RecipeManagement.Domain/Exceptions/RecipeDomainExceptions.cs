namespace RecipeManagement.Domain.Exceptions;

public class RecipeDomainException(string message) : InvalidOperationException(message);

public sealed class RecipeValidationException(string message) : RecipeDomainException(message);

public sealed class RecipeLifecycleException(string message) : RecipeDomainException(message);

public sealed class RecipeConflictException(string message) : RecipeDomainException(message);
