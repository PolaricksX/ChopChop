using System.Net;
using MealPlanner.IntegrationTests.Support;

namespace MealPlanner.IntegrationTests;

public sealed class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task Protected_request_without_token_is_401()
    {
        using var response = await client.GetAsync("/api/v1/meal-plans");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_token_without_required_scope_is_403()
    {
        using var request = factory.Request(
            HttpMethod.Get,
            "/api/v1/meal-plans",
            factory.Jwt.Create("subject", scope: "recipes.read"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Token_with_a_different_scope_is_403()
    {
        using var request = factory.Request(
            HttpMethod.Get,
            "/api/v1/meal-plans",
            factory.Jwt.Create("subject", scope: "recipes.read"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_expired_wrong_issuer_and_wrong_audience_tokens_are_rejected()
    {
        var invalidTokens = new[]
        {
            "not-a-jwt",
            factory.Jwt.Create("subject", lifetime: TimeSpan.FromMinutes(-10)),
            factory.Jwt.Create("subject", issuer: "https://evil.example"),
            factory.Jwt.Create("subject", audience: "another-api"),
        };

        foreach (var token in invalidTokens)
        {
            using var request = factory.Request(HttpMethod.Get, "/api/v1/meal-plans", token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Read_scope_succeeds_and_query_token_is_not_accepted()
    {
        using var request = factory.Request(
            HttpMethod.Get,
            "/api/v1/meal-plans",
            factory.Jwt.Create("subject", scope: "mealplans.read"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var queryToken = await client.GetAsync(
            $"/api/v1/meal-plans?access_token={factory.Jwt.Create("subject")}");
        Assert.Equal(HttpStatusCode.Unauthorized, queryToken.StatusCode);
    }

    [Fact]
    public async Task Invalid_signature_is_rejected_by_real_bearer_validation()
    {
        using var request = factory.Request(
            HttpMethod.Get,
            "/api/v1/meal-plans",
            factory.Jwt.Create("subject", signWithUnknownKey: true));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_anonymous_and_checks_the_database()
    {
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Per_user_rate_limit_returns_429_and_does_not_limit_health()
    {
        await using var limited = new ApiFactory { PermitLimit = 1 };
        await ((IAsyncLifetime)limited).InitializeAsync();
        using var limitedClient = limited.CreateClient();
        var token = limited.Jwt.Create("limited-user");

        using var firstRequest = limited.Request(HttpMethod.Get, "/api/v1/meal-plans", token);
        using var first = await limitedClient.SendAsync(firstRequest);
        using var secondRequest = limited.Request(HttpMethod.Get, "/api/v1/meal-plans", token);
        using var second = await limitedClient.SendAsync(secondRequest);
        using var health = await limitedClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
