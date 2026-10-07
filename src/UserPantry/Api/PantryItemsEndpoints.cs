using UserPantry.Api.Contracts;
using UserPantry.Application.Models;
using UserPantry.Application.Queries;

namespace UserPantry.Api;

public static class PantryItemsEndpoints
{
    public static void MapPantryItems(this WebApplication app)
    {
        app.MapGet(
                "/api/v1/pantry-items/expiring",
                async (
                    int? days,
                    int? page,
                    int? pageSize,
                    GetExpiringPantryItems query,
                    CancellationToken cancellationToken) =>
                {
                    var result = await query.ExecuteAsync(
                        days ?? GetExpiringPantryItems.DefaultDays,
                        page ?? 1,
                        pageSize ?? 20,
                        cancellationToken);

                    return TypedResults.Ok(MapResult(result));
                })
            .RequireAuthorization("pantry.read")
            .WithName("GetExpiringPantryItems")
            .WithSummary("Get expiring pantry items")
            .WithDescription("Returns the authenticated user's pantry items expiring from today through the requested number of days.")
            .Produces<PagedResult<PantryItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static PagedResult<PantryItemResponse> MapResult(
        PagedResult<Domain.PantryItem> result)
    {
        return new PagedResult<PantryItemResponse>(
            result.Items
                .Select(item => new PantryItemResponse(
                    item.Id,
                    item.OwnerId,
                    item.Name,
                    item.Quantity,
                    item.Unit,
                    item.Expiration))
                .ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
