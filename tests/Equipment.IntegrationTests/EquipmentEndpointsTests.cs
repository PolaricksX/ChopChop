using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Equipment.Domain.Entities;
using Equipment.Domain.Enums;
using Equipment.IntegrationTests.Support;
using Equipment.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Equipment.IntegrationTests;

public sealed class EquipmentEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task List_uses_the_validated_subject_as_owner_and_returns_paged_dtos()
    {
        var now = DateTimeOffset.UtcNow;
        await factory.SeedAsync(
            new EquipmentItem(
                Guid.NewGuid(),
                "owner-one",
                "Cast iron pan",
                EquipmentType.Cookware,
                1,
                now),
            new EquipmentItem(
                Guid.NewGuid(),
                "owner-two",
                "Baking tray",
                EquipmentType.Bakeware,
                2,
                now));

        using var request = factory.Request(
            HttpMethod.Get,
            "/api/v1/equipment",
            factory.Jwt.Create("owner-one", "equipment.read"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal("Cast iron pan", item.GetProperty("name").GetString());
        Assert.False(item.TryGetProperty("ownerId", out _));
    }

    [Fact]
    public async Task List_filters_by_type_and_rejects_unbounded_page_size()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = $"filter-owner-{Guid.NewGuid():N}";
        await factory.SeedAsync(
            new EquipmentItem(
                Guid.NewGuid(),
                owner,
                "Saucepan",
                EquipmentType.Cookware,
                1,
                now),
            new EquipmentItem(
                Guid.NewGuid(),
                owner,
                "Sheet pan",
                EquipmentType.Bakeware,
                1,
                now));

        using var filtered = factory.Request(
            HttpMethod.Get,
            "/api/v1/equipment?type=Cookware",
            factory.Jwt.Create(owner, "equipment.read"));
        using var filteredResponse = await client.SendAsync(filtered);
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        var page = await filteredResponse.Content.ReadFromJsonAsync<EquipmentPageResponse>(JsonOptions);
        Assert.NotNull(page);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(EquipmentType.Cookware, Assert.Single(page.Items).Type);

        using var oversized = factory.Request(
            HttpMethod.Get,
            "/api/v1/equipment?pageSize=101",
            factory.Jwt.Create(owner, "equipment.read"));
        using var oversizedResponse = await client.SendAsync(oversized);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedResponse.StatusCode);
        Assert.Contains(
            "application/problem+json",
            oversizedResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Cors_preflight_allows_configured_client_and_etag_headers()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/equipment");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "PUT");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,if-match");

        using var response = await client.SendAsync(request);

        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(
            "If-Match",
            response.Headers.GetValues("Access-Control-Allow-Headers").Single(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_is_idempotent_and_stages_one_outbox_event()
    {
        var owner = $"create-owner-{Guid.NewGuid():N}";
        var token = factory.Jwt.Create(owner, "equipment.write");
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString("N");
        var payload = new
        {
            name = $"Pan {Guid.NewGuid():N}",
            type = "Cookware",
            quantity = 2,
            condition = "Good",
            brand = "ChopChop",
        };

        using var first = factory.Request(HttpMethod.Post, "/api/v1/equipment", token, payload);
        first.Headers.Add("Idempotency-Key", idempotencyKey);
        first.Headers.Add("X-Correlation-Id", correlationId);
        using var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstJson = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstId = firstJson.GetProperty("id").GetGuid();

        using var replay = factory.Request(HttpMethod.Post, "/api/v1/equipment", token, payload);
        replay.Headers.Add("Idempotency-Key", idempotencyKey);
        replay.Headers.Add("X-Correlation-Id", correlationId);
        using var replayResponse = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        var replayJson = await replayResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstId, replayJson.GetProperty("id").GetGuid());

        using var conflictingReplay = factory.Request(
            HttpMethod.Post,
            "/api/v1/equipment",
            token,
            new { payload.name, type = payload.type, quantity = 3, condition = payload.condition });
        conflictingReplay.Headers.Add("Idempotency-Key", idempotencyKey);
        using var conflictResponse = await client.SendAsync(conflictingReplay);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
        Assert.Equal(1, await context.OutboxMessages.CountAsync(message => message.CorrelationId == correlationId));
    }

    [Fact]
    public async Task Create_validates_required_headers_and_duplicate_identity()
    {
        var owner = $"validation-owner-{Guid.NewGuid():N}";
        var token = factory.Jwt.Create(owner, "equipment.write");
        var payload = new
        {
            name = $"Knife set {Guid.NewGuid():N}",
            type = "Knives",
            quantity = 1,
            condition = "Good",
        };

        using var missingKey = factory.Request(HttpMethod.Post, "/api/v1/equipment", token, payload);
        using var missingKeyResponse = await client.SendAsync(missingKey);
        Assert.Equal(HttpStatusCode.BadRequest, missingKeyResponse.StatusCode);

        using var first = factory.Request(HttpMethod.Post, "/api/v1/equipment", token, payload);
        first.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicate = factory.Request(HttpMethod.Post, "/api/v1/equipment", token, payload);
        duplicate.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var duplicateResponse = await client.SendAsync(duplicate);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Non_owner_cannot_update_an_equipment_item()
    {
        var id = Guid.NewGuid();
        await factory.SeedAsync(new EquipmentItem(
            id,
            "actual-owner",
            "Private pan",
            EquipmentType.Cookware,
            1,
            DateTimeOffset.UtcNow));
        using var request = factory.Request(
            HttpMethod.Put,
            $"/api/v1/equipment/{id}",
            factory.Jwt.Create("different-owner", "equipment.write"),
            new
            {
                name = "Changed pan",
                type = "Cookware",
                quantity = 1,
                condition = "Good",
            });
        request.Headers.TryAddWithoutValidation("If-Match", "\"AQIDBAUGBwg=\"");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record EquipmentPageResponse(
        IReadOnlyList<EquipmentItemResponse> Items,
        int Page,
        int PageSize,
        int TotalCount);

    private sealed record EquipmentItemResponse(
        Guid Id,
        string Name,
        EquipmentType Type,
        int Quantity,
        EquipmentCondition Condition,
        EquipmentAvailability Availability);
}
