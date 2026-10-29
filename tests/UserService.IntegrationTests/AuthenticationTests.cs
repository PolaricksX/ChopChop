using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UserService.IntegrationTests.Support;

namespace UserService.IntegrationTests;

/// <summary>Exercises the framework JWT bearer handler and scope policies with test signing credentials.</summary>
public class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Users = "/api/v1/users";
    private readonly HttpClient client = factory.CreateClient();

    private static object ValidBody() => new { username = "u" + Guid.NewGuid().ToString("N")[..12], email = $"{Guid.NewGuid():N}@example.com" };

    private async Task<HttpStatusCode> PostAsync(string? token)
    {
        using var request = factory.Request(HttpMethod.Post, Users, token, ValidBody(), Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private async Task<HttpStatusCode> GetAsync(string? token)
    {
        using var request = factory.Request(HttpMethod.Get, $"{Users}/{Guid.NewGuid()}", token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task Protected_request_without_a_token_is_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await PostAsync(null));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(null));
    }

    [Fact]
    public async Task Malformed_token_is_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync("not-a-jwt"));

    [Fact]
    public async Task Token_with_invalid_signature_is_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(factory.Jwt.Create("s1", signWithUnknownKey: true)));

    [Fact]
    public async Task Expired_token_is_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(factory.Jwt.Create("s1", lifetime: TimeSpan.FromMinutes(-10))));

    [Fact]
    public async Task Token_with_wrong_issuer_is_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(factory.Jwt.Create("s1", issuer: "https://evil.example")));

    [Fact]
    public async Task Token_with_wrong_audience_is_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await GetAsync(factory.Jwt.Create("s1", audience: "another-api")));

    [Fact]
    public async Task Token_in_query_string_is_not_accepted()
    {
        var token = factory.Jwt.Create("s1");

        using var response = await client.GetAsync($"{Users}/{Guid.NewGuid()}?access_token={token}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_token_without_required_scope_is_403()
    {
        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(factory.Jwt.Create("s1", scope: null)));
        Assert.Equal(HttpStatusCode.Forbidden, await GetAsync(factory.Jwt.Create("s1", scope: null)));
    }

    [Fact]
    public async Task Token_with_a_different_scope_is_403()
    {
        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(factory.Jwt.Create("s1", scope: "users.read")));
        Assert.Equal(HttpStatusCode.Forbidden, await GetAsync(factory.Jwt.Create("s1", scope: "users.write")));
        Assert.Equal(HttpStatusCode.Forbidden, await GetAsync(factory.Jwt.Create("s1", scope: "recipes.read")));
    }

    [Fact]
    public async Task Token_with_the_required_scope_succeeds()
    {
        Assert.Equal(HttpStatusCode.Created, await PostAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), scope: "users.write")));
        Assert.Equal(HttpStatusCode.NotFound, await GetAsync(factory.Jwt.Create("s1", scope: "users.read")));
    }

    [Fact]
    public async Task Health_endpoint_is_public_and_checks_the_database()
    {
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unauthorized_response_is_problem_details_with_correlation_id()
    {
        using var request = factory.Request(HttpMethod.Get, $"{Users}/{Guid.NewGuid()}");
        request.Headers.Add("X-Correlation-Id", "trace-abc-123");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("trace-abc-123", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Rate_limit_returns_429_after_the_permit_limit()
    {
        await using var limited = new ApiFactory { PermitLimit = 2 };
        await ((IAsyncLifetime)limited).InitializeAsync();
        using var limitedClient = limited.CreateClient();
        var token = limited.Jwt.Create("rate-limited-user");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var request = limited.Request(HttpMethod.Get, $"{Users}/{Guid.NewGuid()}", token);
            using var response = await limitedClient.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);

        using var health = await limitedClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
