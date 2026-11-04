using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MealPlanner.Api.Contracts;
using MealPlanner.Data.Context;
using MealPlanner.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MealPlanner.IntegrationTests;

public sealed class MealPlanEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Plans = "/api/v1/meal-plans";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
    private readonly HttpClient client = factory.CreateClient();

    private sealed record Caller(string Subject, string Token);

    private Caller NewCaller()
    {
        var subject = Guid.NewGuid().ToString();
        return new Caller(subject, factory.Jwt.Create(subject));
    }

    private async Task<HttpResponseMessage> SendAsync(
        Caller caller,
        HttpMethod method,
        string path,
        object? body = null,
        string? key = null,
        string? ifMatch = null,
        string? correlationId = null)
    {
        using var request = factory.Request(method, path, caller.Token, body, key, ifMatch, correlationId);
        return await client.SendAsync(request);
    }

    private async Task<MealPlanResponse> CreatePlanAsync(Caller caller, string? name = null)
    {
        using var response = await SendAsync(
            caller,
            HttpMethod.Post,
            Plans,
            new
            {
                name = name ?? "Week",
                startDate = "2026-11-01",
                endDate = "2026-11-07",
            },
            Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MealPlanResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task Create_persists_plan_and_correlated_event_in_one_outbox()
    {
        var caller = NewCaller();
        const string correlationId = "meal-plan-trace-21";
        using var response = await SendAsync(
            caller,
            HttpMethod.Post,
            Plans,
            new { name = "Week", startDate = "2026-11-01", endDate = "2026-11-07" },
            "plan-create-21",
            correlationId: correlationId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<MealPlanResponse>(JsonOptions))!;
        Assert.Equal($"{Plans}/{created.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal($"\"{created.Version}\"", response.Headers.ETag!.Tag);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
        Assert.True(await context.MealPlans.AnyAsync(plan => plan.Id == created.Id && plan.OwnerId == caller.Subject));
        var message = await context.OutboxMessages.SingleAsync(item => item.CorrelationId == correlationId);
        Assert.Equal("MealPlanChanged", message.EventType);
        Assert.Equal(1, message.SchemaVersion);
        Assert.Equal(correlationId, message.CorrelationId);
        Assert.Contains(caller.Subject, message.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repeating_create_with_same_idempotency_key_returns_original_plan()
    {
        var caller = NewCaller();
        var body = new { name = "Week", startDate = "2026-11-01", endDate = "2026-11-07" };

        using var first = await SendAsync(caller, HttpMethod.Post, Plans, body, "same-key");
        using var second = await SendAsync(caller, HttpMethod.Post, Plans, body, "same-key");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstPlan = (await first.Content.ReadFromJsonAsync<MealPlanResponse>(JsonOptions))!;
        var secondPlan = (await second.Content.ReadFromJsonAsync<MealPlanResponse>(JsonOptions))!;
        Assert.Equal(firstPlan.Id, secondPlan.Id);
    }

    [Fact]
    public async Task Post_without_idempotency_key_is_400()
    {
        using var response = await SendAsync(
            NewCaller(),
            HttpMethod.Post,
            Plans,
            new { name = "Week", startDate = "2026-11-01", endDate = "2026-11-07" },
            correlationId: "missing-key-trace");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            "missing-key-trace",
            problem.RootElement.GetProperty("correlationId").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Idempotency-Key", out _));
    }

    [Fact]
    public async Task Caller_cannot_read_or_change_another_users_plan()
    {
        var owner = NewCaller();
        var stranger = NewCaller();
        var plan = await CreatePlanAsync(owner);

        using var get = await SendAsync(stranger, HttpMethod.Get, $"{Plans}/{plan.Id}");
        using var put = await SendAsync(
            stranger,
            HttpMethod.Put,
            $"{Plans}/{plan.Id}",
            new { name = "Stolen", startDate = "2026-11-01", endDate = "2026-11-07", version = plan.Version });
        using var delete = await SendAsync(
            stranger,
            HttpMethod.Delete,
            $"{Plans}/{plan.Id}",
            ifMatch: $"\"{plan.Version}\"");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Plan_pagination_and_size_limits_are_enforced()
    {
        var caller = NewCaller();
        await CreatePlanAsync(caller, "One");
        await CreatePlanAsync(caller, "Two");

        using var firstPage = await SendAsync(
            caller,
            HttpMethod.Get,
            $"{Plans}?page=1&pageSize=1");
        using var tooLarge = await SendAsync(caller, HttpMethod.Get, $"{Plans}?pageSize=101");

        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        var page = (await firstPage.Content.ReadFromJsonAsync<MealPlanPageResponse>(JsonOptions))!;
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
    }

    [Fact]
    public async Task Stale_plan_version_returns_conflict()
    {
        var caller = NewCaller();
        var plan = await CreatePlanAsync(caller);
        var update = new
        {
            name = "Updated",
            startDate = "2026-11-01",
            endDate = "2026-11-07",
            version = plan.Version,
        };

        using var first = await SendAsync(caller, HttpMethod.Put, $"{Plans}/{plan.Id}", update);
        using var stale = await SendAsync(caller, HttpMethod.Put, $"{Plans}/{plan.Id}", update);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Multiple_snacks_are_allowed_but_duplicate_breakfast_conflicts()
    {
        var caller = NewCaller();
        var plan = await CreatePlanAsync(caller);
        var snacksPath = $"{Plans}/{plan.Id}/meals";
        var snack = new
        {
            date = "2026-11-01",
            slot = "Snack",
            title = "Fruit",
            recipeId = (Guid?)null,
            servings = 1,
            version = plan.Version,
        };

        using var firstSnack = await SendAsync(caller, HttpMethod.Post, snacksPath, snack, "snack-1");
        var firstEntry = (await firstSnack.Content.ReadFromJsonAsync<MealEntryResponse>(JsonOptions))!;
        using var secondSnack = await SendAsync(
            caller,
            HttpMethod.Post,
            snacksPath,
            snack with { title = "Yogurt", version = firstEntry.Version },
            "snack-2");
        var secondEntry = (await secondSnack.Content.ReadFromJsonAsync<MealEntryResponse>(JsonOptions))!;
        var breakfast = new
        {
            date = "2026-11-01",
            slot = "Breakfast",
            title = "Oats",
            recipeId = (Guid?)null,
            servings = 1,
            version = secondEntry.Version,
        };
        using var firstBreakfast = await SendAsync(caller, HttpMethod.Post, snacksPath, breakfast, "breakfast-1");
        var breakfastEntry = (await firstBreakfast.Content.ReadFromJsonAsync<MealEntryResponse>(JsonOptions))!;
        using var duplicateBreakfast = await SendAsync(
            caller,
            HttpMethod.Post,
            snacksPath,
            breakfast with { title = "Eggs", version = breakfastEntry.Version },
            "breakfast-2");

        Assert.Equal(HttpStatusCode.Created, firstSnack.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondSnack.StatusCode);
        Assert.Equal(HttpStatusCode.Created, firstBreakfast.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateBreakfast.StatusCode);
    }

    [Fact]
    public async Task Planned_meal_can_be_updated_and_progressed_through_valid_statuses()
    {
        var caller = NewCaller();
        var plan = await CreatePlanAsync(caller);
        var mealsPath = $"{Plans}/{plan.Id}/meals";
        using var created = await SendAsync(
            caller,
            HttpMethod.Post,
            mealsPath,
            new
            {
                date = "2026-11-01",
                slot = "Breakfast",
                title = "Oats",
                recipeId = (Guid?)null,
                servings = 1,
                version = plan.Version,
            },
            "meal-add");
        var entry = (await created.Content.ReadFromJsonAsync<MealEntryResponse>(JsonOptions))!;

        using var updated = await SendAsync(
            caller,
            HttpMethod.Put,
            $"{mealsPath}/{entry.Id}",
            new
            {
                date = "2026-11-02",
                slot = "Lunch",
                title = "Lentil soup",
                recipeId = Guid.NewGuid(),
                servings = 2,
                version = entry.Version,
            });
        var revised = (await updated.Content.ReadFromJsonAsync<MealEntryResponse>(JsonOptions))!;
        using var inProgress = await SendAsync(
            caller,
            HttpMethod.Patch,
            $"{mealsPath}/{entry.Id}/status",
            new { status = "InProgress", version = revised.Version });
        var progressing = (await inProgress.Content.ReadFromJsonAsync<MealEntryResponse>(JsonOptions))!;
        using var completed = await SendAsync(
            caller,
            HttpMethod.Patch,
            $"{mealsPath}/{entry.Id}/status",
            new { status = "Completed", version = progressing.Version });
        var finished = (await completed.Content.ReadFromJsonAsync<MealEntryResponse>(JsonOptions))!;
        using var invalidTransition = await SendAsync(
            caller,
            HttpMethod.Patch,
            $"{mealsPath}/{entry.Id}/status",
            new { status = "Skipped", version = finished.Version });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Lentil soup", revised.Title);
        Assert.Equal(2, revised.Servings);
        Assert.Equal(HttpStatusCode.OK, inProgress.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTransition.StatusCode);
    }

    [Fact]
    public async Task Planned_meal_can_be_removed_with_latest_plan_version()
    {
        var caller = NewCaller();
        var plan = await CreatePlanAsync(caller);
        var mealsPath = $"{Plans}/{plan.Id}/meals";
        using var created = await SendAsync(
            caller,
            HttpMethod.Post,
            mealsPath,
            new
            {
                date = "2026-11-01",
                slot = "Dinner",
                title = "Vegetable curry",
                recipeId = (Guid?)null,
                servings = 3,
                version = plan.Version,
            },
            "meal-remove");
        var entry = (await created.Content.ReadFromJsonAsync<MealEntryResponse>(JsonOptions))!;

        using var removed = await SendAsync(
            caller,
            HttpMethod.Delete,
            $"{mealsPath}/{entry.Id}",
            ifMatch: $"\"{entry.Version}\"");
        using var listed = await SendAsync(caller, HttpMethod.Get, mealsPath);
        var page = (await listed.Content.ReadFromJsonAsync<MealEntryPageResponse>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Plan_status_requires_allowed_transition_and_delete_requires_latest_etag()
    {
        var caller = NewCaller();
        var plan = await CreatePlanAsync(caller);
        using var invalidTransition = await SendAsync(
            caller,
            HttpMethod.Patch,
            $"{Plans}/{plan.Id}/status",
            new { status = "Completed", version = plan.Version });
        using var activate = await SendAsync(
            caller,
            HttpMethod.Patch,
            $"{Plans}/{plan.Id}/status",
            new { status = "Active", version = plan.Version });

        Assert.Equal(HttpStatusCode.BadRequest, invalidTransition.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        var active = (await activate.Content.ReadFromJsonAsync<MealPlanResponse>(JsonOptions))!;
        using var delete = await SendAsync(
            caller,
            HttpMethod.Delete,
            $"{Plans}/{plan.Id}",
            ifMatch: $"\"{active.Version}\"");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Development_openapi_contains_versioned_meal_plan_operations()
    {
        using var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/v1/meal-plans", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/meal-plans/{planId}/meals/{entryId}/status", document, StringComparison.Ordinal);
    }
}
