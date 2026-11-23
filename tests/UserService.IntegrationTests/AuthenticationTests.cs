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

        using var byId = await client.GetAsync($"{Users}/{Guid.NewGuid()}?access_token={token}");
        using var me = await client.GetAsync($"{Users}/me?access_token={token}");

        Assert.Equal(HttpStatusCode.Unauthorized, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
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

    [Theory]
    [InlineData("POST", "/api/v1/users", "users.write")]
    [InlineData("GET", "/api/v1/users/{id}", "users.read")]
    [InlineData("GET", "/api/v1/users/me", "users.read")]
    [InlineData("PUT", "/api/v1/users/{id}", "users.write")]
    [InlineData("DELETE", "/api/v1/users/{id}", "users.write")]
    [InlineData("PUT", "/api/v1/users/{id}/profile-picture", "users.write")]
    public async Task Every_route_enforces_authentication_scope_direction_and_subject(string method, string route, string requiredScope)
    {
        var url = route.Replace("{id}", Guid.NewGuid().ToString());
        var wrongDirectionScope = requiredScope == "users.read" ? "users.write" : "users.read";

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(method, url, null));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(method, url, factory.Jwt.Create("s1", "recipes.read")));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(method, url, factory.Jwt.Create("s1", wrongDirectionScope)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(method, url, factory.Jwt.Create(null, requiredScope)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(method, url, factory.Jwt.Create(new string('x', 257), requiredScope)));
    }

    private async Task<HttpStatusCode> StatusAsync(string method, string url, string? token)
    {
        using var request = factory.Request(new HttpMethod(method), url, token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
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

    [Fact]
    public async Task Profile_picture_route_rejects_bad_tokens_with_401_and_missing_scope_with_403()
    {
        var url = $"{Users}/{Guid.NewGuid()}/profile-picture";

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync("PUT", url, "not-a-jwt"));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync("PUT", url, factory.Jwt.Create("s1", signWithUnknownKey: true)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync("PUT", url, factory.Jwt.Create("s1", lifetime: TimeSpan.FromMinutes(-10))));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync("PUT", url, factory.Jwt.Create("s1", issuer: "https://evil.example")));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync("PUT", url, factory.Jwt.Create("s1", audience: "another-api")));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync("PUT", url, factory.Jwt.Create("s1", scope: null)));
    }

    [Fact]
    public async Task Profile_picture_route_with_users_write_passes_auth_and_hides_other_users_profiles()
    {
        using var request = factory.Request(
            HttpMethod.Put,
            $"{Users}/{Guid.NewGuid()}/profile-picture",
            factory.Jwt.Create(Guid.NewGuid().ToString(), scope: "users.write"));
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "picture");
        request.Content = form;

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
