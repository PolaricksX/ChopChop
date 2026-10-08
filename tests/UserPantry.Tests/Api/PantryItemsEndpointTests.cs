using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserPantry.Api.Contracts;
using UserPantry.Application.Models;
using UserPantry.Domain;
using UserPantry.Infrastructure;
using Xunit;

namespace UserPantry.Tests.Api;

public sealed class PantryItemsEndpointTests(
    CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        await factory.InitializeDatabaseAsync();
        await factory.ClearItemsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [RequiresSqlServerFact]
    public async Task OmittedDays_UsesThreeDayWindow()
    {
        await SeedAsync(
            Item(1, CustomWebApplicationFactory.DefaultUserId, "Day 3", 3),
            Item(2, CustomWebApplicationFactory.DefaultUserId, "Day 4", 4));

        var response = await GetAsync();
        var page = await ReadPageAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(page.Items);
        Assert.Equal("Day 3", page.Items[0].Name);
    }

    [RequiresSqlServerFact]
    public async Task IncludesItemOnDayN()
    {
        await SeedAsync(Item(1, CustomWebApplicationFactory.DefaultUserId, "Day 5", 5));

        var response = await GetAsync("?days=5");
        var page = await ReadPageAsync(response);

        Assert.Single(page.Items);
    }

    [RequiresSqlServerFact]
    public async Task ZeroDaysIncludesToday()
    {
        await SeedAsync(Item(1, CustomWebApplicationFactory.DefaultUserId, "Today", 0));

        var response = await GetAsync("?days=0");
        var page = await ReadPageAsync(response);

        Assert.Single(page.Items);
    }

    [RequiresSqlServerFact]
    public async Task ExcludesAlreadyExpiredAndUnscheduledItems()
    {
        await SeedAsync(
            Item(1, CustomWebApplicationFactory.DefaultUserId, "Expired", -1),
            Item(2, CustomWebApplicationFactory.DefaultUserId, "No date", null));

        var response = await GetAsync();
        var page = await ReadPageAsync(response);

        Assert.Empty(page.Items);
    }

    [RequiresSqlServerTheory]
    [InlineData("?days=not-a-number")]
    [InlineData("?days=-1")]
    [InlineData("?days=366")]
    [InlineData("?pageSize=101")]
    public async Task InvalidParameters_ReturnBadRequest(string query)
    {
        var response = await GetAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresSqlServerFact]
    public async Task OmittedPageSize_UsesTwenty()
    {
        await SeedAsync(Enumerable.Range(0, 21)
            .Select(index => Item(index, CustomWebApplicationFactory.DefaultUserId, $"Item {index}", index % 4))
            .ToArray());

        var page = await ReadPageAsync(await GetAsync());

        Assert.Equal(20, page.PageSize);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal(21, page.TotalCount);
    }

    [RequiresSqlServerFact]
    public async Task Results_AreOrderedByExpirationNameAndId()
    {
        var sameDate = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        await SeedAsync(
            Item(3, CustomWebApplicationFactory.DefaultUserId, "Same", 1, sameDate),
            Item(2, CustomWebApplicationFactory.DefaultUserId, "Same", 1, sameDate),
            Item(1, CustomWebApplicationFactory.DefaultUserId, "Alpha", 1, sameDate),
            Item(4, CustomWebApplicationFactory.DefaultUserId, "Earlier", 0));

        var page = await ReadPageAsync(await GetAsync());

        Assert.Equal(["Earlier", "Alpha", "Same", "Same"], page.Items.Select(item => item.Name));
        Assert.Equal(
            [4, 1, 2, 3],
            page.Items.Select(item => int.Parse(item.Id.ToString()[^2..])));
    }

    [RequiresSqlServerFact]
    public async Task Results_AreScopedToCaller_AndOwnerQueryIsIgnored()
    {
        await SeedAsync(
            Item(1, CustomWebApplicationFactory.DefaultUserId, "Mine", 1),
            Item(2, CustomWebApplicationFactory.OtherUserId, "Not mine", 1));

        var response = await GetAsync(
            $"?ownerId={CustomWebApplicationFactory.OtherUserId}");
        var page = await ReadPageAsync(response);

        Assert.Single(page.Items);
        Assert.Equal("Mine", page.Items[0].Name);
    }

    [RequiresSqlServerFact]
    public async Task UnauthenticatedRequest_ReturnsUnauthorized()
    {
        var response = await client.GetAsync("/api/v1/pantry-items/expiring");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresSqlServerFact]
    public async Task InvalidDaysProblem_IncludesCorrelationIdAndOmitsStackTrace()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/pantry-items/expiring?days=366");
        request.Headers.Add("X-Test-User", CustomWebApplicationFactory.DefaultUserId.ToString());
        request.Headers.Add("X-Correlation-Id", "endpoint-test-correlation");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"correlationId\":\"endpoint-test-correlation\"", body);
        Assert.DoesNotContain("StackTrace", body);
        Assert.DoesNotContain("at UserPantry.", body);
    }

    private async Task<HttpResponseMessage> GetAsync(string query = "")
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/pantry-items/expiring{query}");
        request.Headers.Add("X-Test-User", CustomWebApplicationFactory.DefaultUserId.ToString());
        return await client.SendAsync(request);
    }

    private async Task SeedAsync(params PantryItem[] items)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PantryDbContext>();
        await db.PantryItems.AddRangeAsync(items);
        await db.SaveChangesAsync();
    }

    private static PantryItem Item(
        int id,
        Guid ownerId,
        string name,
        int? days,
        DateTimeOffset? expiration = null) =>
        new()
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}"),
            OwnerId = ownerId,
            Name = name,
            Quantity = 1,
            Unit = "item",
            Expiration = expiration ?? (days is null
                ? null
                : new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero).AddDays(days.Value))
        };

    private static async Task<PagedResult<PantryItemResponse>> ReadPageAsync(
        HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<PantryItemResponse>>())!;
    }
}
