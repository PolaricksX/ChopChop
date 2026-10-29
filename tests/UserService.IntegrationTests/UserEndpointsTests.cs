using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UserService.Api.Contracts;
using UserService.IntegrationTests.Support;

namespace UserService.IntegrationTests;

public class UserEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Users = "/api/v1/users";
    private readonly HttpClient client = factory.CreateClient();

    private sealed record Caller(string Subject, string Token);

    private Caller NewCaller()
    {
        var subject = Guid.NewGuid().ToString();
        return new Caller(subject, factory.Jwt.Create(subject));
    }

    private static string UniqueName() => "u" + Guid.NewGuid().ToString("N")[..12];

    private static object NewBody(string? username = null, string? email = null, string? displayName = "Display") =>
        new { username = username ?? UniqueName(), email = email ?? $"{Guid.NewGuid():N}@example.com", displayName };

    private async Task<HttpResponseMessage> SendAsync(Caller? caller, HttpMethod method, string url, object? body = null, string? key = null)
    {
        using var request = factory.Request(method, url, caller?.Token, body, key);
        return await client.SendAsync(request);
    }

    private async Task<UserResponse> CreateUserAsync(Caller caller, object? body = null)
    {
        using var response = await SendAsync(caller, HttpMethod.Post, Users, body ?? NewBody(), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Post_creates_the_callers_profile_with_201_and_location()
    {
        var caller = NewCaller();
        var body = NewBody("Alice.Test" + Guid.NewGuid().ToString("N")[..6], $"Alice{Guid.NewGuid():N}@Example.com");

        using var response = await SendAsync(caller, HttpMethod.Post, Users, body, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal($"{Users}/{created.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal(created.Email, created.Email.ToLowerInvariant());
        Assert.False(string.IsNullOrEmpty(created.Version));
        Assert.True((DateTimeOffset.UtcNow - created.CreatedAt).Duration() < TimeSpan.FromMinutes(1));
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Post_ignores_client_supplied_server_owned_fields()
    {
        var caller = NewCaller();
        var forgedId = Guid.NewGuid();
        var body = new { id = forgedId, subject = "someone-else", deletedAt = DateTimeOffset.UtcNow, username = UniqueName(), email = $"{Guid.NewGuid():N}@example.com" };

        var created = await CreateUserAsync(caller, body);

        Assert.NotEqual(forgedId, created.Id);
        using var get = await SendAsync(caller, HttpMethod.Get, $"{Users}/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using var forged = await SendAsync(new Caller("someone-else", factory.Jwt.Create("someone-else")), HttpMethod.Get, $"{Users}/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
    }

    [Fact]
    public async Task Post_without_idempotency_key_is_400_problem_with_correlation_id()
    {
        using var response = await SendAsync(NewCaller(), HttpMethod.Post, Users, NewBody());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Idempotency-Key", out _));
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), problem.GetProperty("correlationId").GetString());
    }

    [Theory]
    [InlineData("ab", "alice@example.com", "username")]
    [InlineData("alice_ok", "not-an-email", "email")]
    [InlineData("al ice", "alice@example.com", "username")]
    [InlineData("alice'; DROP TABLE Users;--", "alice@example.com", "username")]
    public async Task Post_with_invalid_input_is_400_with_field_errors(string username, string email, string field)
    {
        using var response = await SendAsync(NewCaller(), HttpMethod.Post, Users, new { username, email }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True((await ReadJsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Post_with_malformed_json_is_400_not_500()
    {
        using var request = factory.Request(HttpMethod.Post, Users, NewCaller().Token, idempotencyKey: Guid.NewGuid().ToString());
        request.Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_empty_body_is_400()
    {
        using var response = await SendAsync(NewCaller(), HttpMethod.Post, Users, key: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_is_idempotent_for_the_same_key_and_payload_and_conflicts_for_a_different_payload()
    {
        var caller = NewCaller();
        var body = NewBody();
        var key = Guid.NewGuid().ToString();

        using var first = await SendAsync(caller, HttpMethod.Post, Users, body, key);
        using var replay = await SendAsync(caller, HttpMethod.Post, Users, body, key);
        using var different = await SendAsync(caller, HttpMethod.Post, Users, NewBody(), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<UserResponse>())!.Id,
            (await replay.Content.ReadFromJsonAsync<UserResponse>())!.Id);
        Assert.Equal(HttpStatusCode.Conflict, different.StatusCode);
    }

    [Fact]
    public async Task Post_second_profile_for_the_same_account_is_409()
    {
        var caller = NewCaller();
        await CreateUserAsync(caller);

        using var response = await SendAsync(caller, HttpMethod.Post, Users, NewBody(), Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Post_duplicate_email_or_username_is_409_with_descriptive_message()
    {
        var existing = await CreateUserAsync(NewCaller());

        using var sameEmail = await SendAsync(NewCaller(), HttpMethod.Post, Users, NewBody(email: existing.Email.ToUpperInvariant()), Guid.NewGuid().ToString());
        using var sameUsername = await SendAsync(NewCaller(), HttpMethod.Post, Users, NewBody(username: existing.Username.ToUpperInvariant()), Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Conflict, sameEmail.StatusCode);
        Assert.Contains("email", (await ReadJsonAsync(sameEmail)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Conflict, sameUsername.StatusCode);
        Assert.Contains("username", (await ReadJsonAsync(sameUsername)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Get_returns_the_callers_own_profile()
    {
        var caller = NewCaller();
        var created = await CreateUserAsync(caller);

        using var response = await SendAsync(caller, HttpMethod.Get, $"{Users}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, await response.Content.ReadFromJsonAsync<UserResponse>());
    }

    [Fact]
    public async Task Get_of_another_users_profile_or_unknown_id_is_404()
    {
        var owner = await CreateUserAsync(NewCaller());

        using var other = await SendAsync(NewCaller(), HttpMethod.Get, $"{Users}/{owner.Id}");
        using var unknown = await SendAsync(NewCaller(), HttpMethod.Get, $"{Users}/{Guid.NewGuid()}");
        using var badId = await SendAsync(NewCaller(), HttpMethod.Get, $"{Users}/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badId.StatusCode);
        Assert.DoesNotContain(owner.Email, await other.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Put_updates_the_profile_and_issues_a_new_version()
    {
        var caller = NewCaller();
        var created = await CreateUserAsync(caller);
        var update = new { username = UniqueName(), email = $"{Guid.NewGuid():N}@example.com", displayName = "New Name", version = created.Version };

        using var response = await SendAsync(caller, HttpMethod.Put, $"{Users}/{created.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(update.username, updated.Username);
        Assert.Equal("New Name", updated.DisplayName);
        Assert.NotEqual(created.Version, updated.Version);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public async Task Put_with_a_stale_version_is_409()
    {
        var caller = NewCaller();
        var created = await CreateUserAsync(caller);
        object Update(string name) => new { username = name, email = $"{Guid.NewGuid():N}@example.com", version = created.Version };

        using var first = await SendAsync(caller, HttpMethod.Put, $"{Users}/{created.Id}", Update(UniqueName()));
        using var stale = await SendAsync(caller, HttpMethod.Put, $"{Users}/{created.Id}", Update(UniqueName()));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Put_of_another_users_profile_is_404_and_changes_nothing()
    {
        var owner = NewCaller();
        var created = await CreateUserAsync(owner);

        using var response = await SendAsync(NewCaller(), HttpMethod.Put, $"{Users}/{created.Id}",
            new { username = UniqueName(), email = $"{Guid.NewGuid():N}@example.com", version = created.Version });
        using var unchanged = await SendAsync(owner, HttpMethod.Get, $"{Users}/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(created, await unchanged.Content.ReadFromJsonAsync<UserResponse>());
    }

    [Fact]
    public async Task Put_with_invalid_input_or_missing_version_is_400()
    {
        var caller = NewCaller();
        var created = await CreateUserAsync(caller);

        using var badEmail = await SendAsync(caller, HttpMethod.Put, $"{Users}/{created.Id}", new { username = UniqueName(), email = "nope", version = created.Version });
        using var noVersion = await SendAsync(caller, HttpMethod.Put, $"{Users}/{created.Id}", new { username = UniqueName(), email = $"{Guid.NewGuid():N}@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
    }

    [Fact]
    public async Task Put_to_another_users_email_is_409()
    {
        var other = await CreateUserAsync(NewCaller());
        var caller = NewCaller();
        var created = await CreateUserAsync(caller);

        using var response = await SendAsync(caller, HttpMethod.Put, $"{Users}/{created.Id}", new { username = created.Username, email = other.Email, version = created.Version });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_soft_deletes_then_the_profile_is_gone_and_its_unique_values_are_free()
    {
        var caller = NewCaller();
        var created = await CreateUserAsync(caller);

        using var delete = await SendAsync(caller, HttpMethod.Delete, $"{Users}/{created.Id}");
        using var get = await SendAsync(caller, HttpMethod.Get, $"{Users}/{created.Id}");
        using var again = await SendAsync(caller, HttpMethod.Delete, $"{Users}/{created.Id}");
        using var reuse = await SendAsync(NewCaller(), HttpMethod.Post, Users, new { username = created.Username, email = created.Email }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
    }

    [Fact]
    public async Task Delete_of_another_users_profile_is_404_and_leaves_it_intact()
    {
        var owner = NewCaller();
        var created = await CreateUserAsync(owner);

        using var response = await SendAsync(NewCaller(), HttpMethod.Delete, $"{Users}/{created.Id}");
        using var stillThere = await SendAsync(owner, HttpMethod.Get, $"{Users}/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Hostile_display_name_is_stored_as_data_and_does_not_break_queries()
    {
        var caller = NewCaller();
        const string hostile = "x'; DROP TABLE Users;-- <script>alert(1)</script>";

        var created = await CreateUserAsync(caller, NewBody(displayName: hostile));

        using var get = await SendAsync(caller, HttpMethod.Get, $"{Users}/{created.Id}");
        Assert.Equal(hostile, (await get.Content.ReadFromJsonAsync<UserResponse>())!.DisplayName);
    }

    [Fact]
    public async Task Error_responses_do_not_leak_internals()
    {
        using var response = await SendAsync(NewCaller(), HttpMethod.Post, Users, new { username = "x", email = "y" }, Guid.NewGuid().ToString());
        var text = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("StackTrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlException", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Correlation_id_is_echoed_when_valid_and_replaced_when_not()
    {
        var caller = NewCaller();
        using var ok = factory.Request(HttpMethod.Get, $"{Users}/{Guid.NewGuid()}", caller.Token);
        ok.Headers.Add("X-Correlation-Id", "req-42");
        using var bad = factory.Request(HttpMethod.Get, $"{Users}/{Guid.NewGuid()}", caller.Token);
        bad.Headers.Add("X-Correlation-Id", "bad id with spaces & symbols");

        using var okResponse = await client.SendAsync(ok);
        using var badResponse = await client.SendAsync(bad);

        Assert.Equal("req-42", okResponse.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Equal(32, badResponse.Headers.GetValues("X-Correlation-Id").Single().Length);
        Assert.Equal("req-42", (await ReadJsonAsync(okResponse)).GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task OpenAPI_document_describes_the_user_endpoints_in_development()
    {
        using var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/v1/users", document);
        Assert.Contains("/api/v1/users/{id}", document);
        Assert.Contains("CreateUser", document);
    }
}
