using UserPantry.Application.Abstractions;
using UserPantry.Application.Models;
using UserPantry.Application.Queries;
using UserPantry.Domain;
using Xunit;

namespace UserPantry.Tests.Application;

public sealed class GetExpiringPantryItemsTests
{
    [Fact]
    public void CalculatesUtcWindow()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 34, 0, TimeSpan.Zero);

        var window = GetExpiringPantryItems.CalculateUtcWindow(now);

        Assert.Equal(
            new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero),
            window.StartsAtInclusive);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero),
            window.EndsAtExclusive);
    }

    [Fact]
    public async Task UsesTimeProviderAndCurrentUserWhenExecuting()
    {
        var ownerId = Guid.NewGuid();
        var query = new RecordingQuery();
        var currentUser = new StubCurrentUser(ownerId);
        var timeProvider = new FrozenTimeProvider(
            new DateTimeOffset(2026, 10, 6, 12, 34, 0, TimeSpan.Zero));
        var service = new GetExpiringPantryItems(query, currentUser, timeProvider);

        await service.ExecuteAsync();

        Assert.Equal(ownerId, query.OwnerId);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero),
            query.StartsAtInclusive);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero),
            query.EndsAtExclusive);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    private sealed class RecordingQuery : IExpiringPantryItemsQuery
    {
        public Guid OwnerId { get; private set; }
        public DateTimeOffset StartsAtInclusive { get; private set; }
        public DateTimeOffset EndsAtExclusive { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }

        public Task<PagedResult<PantryItem>> ExecuteAsync(
            Guid ownerId,
            DateTimeOffset startsAtInclusive,
            DateTimeOffset endsAtExclusive,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            OwnerId = ownerId;
            StartsAtInclusive = startsAtInclusive;
            EndsAtExclusive = endsAtExclusive;
            Page = page;
            PageSize = pageSize;

            return Task.FromResult(new PagedResult<PantryItem>(
                Array.Empty<PantryItem>(),
                page,
                pageSize,
                0));
        }
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
