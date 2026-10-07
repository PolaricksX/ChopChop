using UserPantry.Application.Abstractions;
using UserPantry.Application.Models;
using UserPantry.Domain;

namespace UserPantry.Application.Queries;

public sealed class GetExpiringPantryItems(
    IExpiringPantryItemsQuery query,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public const int DefaultDays = 3;
    public const int MinimumDays = 0;
    public const int MaximumDays = 365;

    public async Task<PagedResult<PantryItem>> ExecuteAsync(
        int days = DefaultDays,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(days, MinimumDays, MaximumDays, nameof(days));
        ValidateRange(page, 1, int.MaxValue, nameof(page));
        ValidateRange(pageSize, 1, 100, nameof(pageSize));

        var window = CalculateUtcWindow(timeProvider.GetUtcNow(), days);

        return await query.ExecuteAsync(
            currentUser.UserId,
            window.StartsAtInclusive,
            window.EndsAtExclusive,
            page,
            pageSize,
            cancellationToken);
    }

    public static ExpiringPantryItemsWindow CalculateUtcWindow(
        DateTimeOffset utcNow,
        int days = DefaultDays)
    {
        ValidateRange(days, MinimumDays, MaximumDays, nameof(days));

        var today = new DateTimeOffset(
            utcNow.Year,
            utcNow.Month,
            utcNow.Day,
            0,
            0,
            0,
            TimeSpan.Zero);

        return new ExpiringPantryItemsWindow(today, today.AddDays(days + 1));
    }

    private static void ValidateRange(int value, int minimum, int maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {minimum} and {maximum}.");
        }
    }
}

public sealed record ExpiringPantryItemsWindow(
    DateTimeOffset StartsAtInclusive,
    DateTimeOffset EndsAtExclusive);
