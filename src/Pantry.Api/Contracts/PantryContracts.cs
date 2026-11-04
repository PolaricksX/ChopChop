using Pantry.Domain.Enums;

namespace Pantry.Api.Contracts;

public sealed record PantryRequest(
    string Name, PantryType Type, decimal Quantity, Unit Unit,
    string? CustomCategory = null, string? Notes = null, DateTimeOffset? ExpiresAt = null);

public sealed record PantryResponse(
    Guid Id, string Name, PantryType Type, string? CustomCategory, string? Notes,
    decimal Quantity, Unit Unit, DateTimeOffset? ExpiresAt, Availability Availability,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Version);

public sealed record PantryQuery(
    string? Search, PantryType? Type, Availability? Availability,
    DateTimeOffset? ExpiresBefore, int Page, int PageSize)
{
    public static PantryQuery From(HttpRequest request)
    {
        static T? Parse<T>(string? value) where T : struct, Enum =>
            Enum.TryParse<T>(value, true, out var result) ? result : null;
        var page = int.TryParse(request.Query["page"], out var p) ? p : 1;
        var size = int.TryParse(request.Query["pageSize"], out var s) ? s : 20;
        var expiration = DateTimeOffset.TryParse(request.Query["expiresBefore"], out var date) ? date : (DateTimeOffset?)null;
        return new(request.Query["search"], Parse<PantryType>(request.Query["type"]),
            Parse<Availability>(request.Query["availability"]), expiration,
            Math.Clamp(page, 1, 10_000), Math.Clamp(size, 1, 100));
    }
}
