using System.Security.Claims;
using Pantry.Api.Contracts;
using Pantry.Api.Infrastructure;
using Pantry.Data.Repositories;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;

namespace Pantry.Api.Services;

public sealed class PantryApplicationService(IPantryStore store, IHttpContextAccessor httpContext)
{
    private static string Owner(ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("A validated subject claim is required.");

    public async Task<IResult> ListAsync(ClaimsPrincipal user, PantryQuery query, CancellationToken token) =>
        Results.Ok((await store.ListAsync(Owner(user), query, token)).Select(Map));

    public async Task<IResult> AddAsync(ClaimsPrincipal user, PantryRequest request, CancellationToken token)
    {
        var validation = Validate(request);
        if (validation is not null) return Problem(validation);
        try
        {
            var item = await store.AddAsync(new PantryItem(Guid.NewGuid(), Owner(user), request.Name,
                request.Type, request.Quantity, request.Unit, request.Notes, request.ExpiresAt, request.CustomCategory), token);
            return Results.Created($"/api/v1/pantry/{item.Id}", Map(item));
        }
        catch (ArgumentException ex) { return Problem(ex.Message); }
        catch (DuplicatePantryItemException ex) { return Results.Conflict(new { detail = ex.Message }); }
    }

    public async Task<IResult> UpdateAsync(ClaimsPrincipal user, Guid id, PantryRequest request,
        string? ifMatch, CancellationToken token)
    {
        var validation = Validate(request);
        if (validation is not null) return Problem(validation);
        if (!TryVersion(ifMatch, out var version)) return Failure("A valid If-Match version is required.", 428);
        try
        {
            var owner = Owner(user);
            if (!await store.ExistsAsync(owner, id, token)) return Results.NotFound();
            return await store.UpdateAsync(owner, id, request, version!, token) ? Results.NoContent() : Results.Conflict();
        }
        catch (ArgumentException ex) { return Problem(ex.Message); }
        catch (DuplicatePantryItemException ex) { return Results.Conflict(new { detail = ex.Message }); }
    }

    public async Task<IResult> ChangeStateAsync(ClaimsPrincipal user, Guid id, string? ifMatch, bool consume, CancellationToken token)
    {
        if (!TryVersion(ifMatch, out var version)) return Failure("A valid If-Match version is required.", 428);
        try
        {
            var owner = Owner(user);
            if (!await store.ExistsAsync(owner, id, token)) return Results.NotFound();
            return await store.ChangeAsync(owner, id, version, consume, token) ? Results.NoContent() : Results.Conflict();
        }
        catch (InvalidOperationException ex) { return Results.Conflict(new { detail = ex.Message }); }
    }

    public async Task<IResult> DeleteAsync(ClaimsPrincipal user, Guid id, CancellationToken token) =>
        await store.DeleteAsync(Owner(user), id, token) ? Results.NoContent() : Results.NotFound();

    private IResult Problem(string detail) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["request"] = [detail] },
        extensions: Correlation());
    private IResult Failure(string detail, int status) => Results.Problem(detail, statusCode: status, extensions: Correlation());
    private Dictionary<string, object?> Correlation() =>
        new() { ["correlationId"] = httpContext.HttpContext?.TraceIdentifier };
    private static string? Validate(PantryRequest request) =>
        string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 ? "Name is required and must be at most 120 characters." :
        !Enum.IsDefined(request.Type) ? "A valid pantry type is required." :
        request.Type == PantryType.Custom && string.IsNullOrWhiteSpace(request.CustomCategory) ? "Custom category is required for custom items." :
        request.Quantity <= 0 ? "Quantity must be positive." : null;
    private static bool TryVersion(string? value, out byte[]? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { version = Convert.FromBase64String(value.Trim('"')); return version.Length > 0; } catch (FormatException) { return false; }
    }
    private static PantryResponse Map(PantryItem item) => new(item.Id, item.Name, item.Type, item.CustomCategory,
        item.Notes, item.Quantity, item.Unit, item.ExpiresAt, item.Availability, item.CreatedAt, item.UpdatedAt,
        Convert.ToBase64String(item.Version));
}
