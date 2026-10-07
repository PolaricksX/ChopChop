namespace UserPantry.Api.Contracts;

/// <summary>Represents a pantry item returned by the User Pantry API.</summary>
public sealed record PantryItemResponse(
    Guid Id,
    Guid OwnerId,
    string Name,
    decimal Quantity,
    string Unit,
    DateTimeOffset? Expiration);
