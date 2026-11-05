using System.Net;
using Equipment.IntegrationTests.Support;

namespace Equipment.IntegrationTests;

public sealed class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task Protected_request_without_a_token_is_unauthorized()
    {
        using var response = await client.GetAsync("/api/v1/equipment");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_token_signature_issuer_audience_and_expiry_are_rejected()
    {
        var invalidTokens = new[]
        {
            "not-a-jwt",
            factory.Jwt.Create("subject", signWithUnknownKey: true),
            factory.Jwt.Create("subject", issuer: "https://attacker.example"),
            factory.Jwt.Create("subject", audience: "another-api"),
            factory.Jwt.Create("subject", lifetime: TimeSpan.FromMinutes(-5)),
        };

        foreach (var token in invalidTokens)
        {
            using var request = factory.Request(HttpMethod.Get, "/api/v1/equipment", token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Read_and_write_scopes_are_enforced_per_operation()
    {
        using var readOnWrite = factory.Request(
            HttpMethod.Post,
            "/api/v1/equipment",
            factory.Jwt.Create("reader", "equipment.read"),
            new { name = "Pan" });
        using var readDenied = await client.SendAsync(readOnWrite);
        Assert.Equal(HttpStatusCode.Forbidden, readDenied.StatusCode);

        using var writeOnRead = factory.Request(
            HttpMethod.Get,
            "/api/v1/equipment",
            factory.Jwt.Create("writer", "equipment.write"));
        using var writeDenied = await client.SendAsync(writeOnRead);
        Assert.Equal(HttpStatusCode.Forbidden, writeDenied.StatusCode);

        using var readAllowed = factory.Request(
            HttpMethod.Get,
            "/api/v1/equipment",
            factory.Jwt.Create("reader", "equipment.read"));
        using var readResponse = await client.SendAsync(readAllowed);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_is_anonymous_and_checks_the_database()
    {
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rate_limit_applies_per_subject_but_not_to_health()
    {
        await using var limitedFactory = new ApiFactory { PermitLimit = 2 };
        using var limitedClient = limitedFactory.CreateClient();
        var token = limitedFactory.Jwt.Create("rate-limited-subject", "equipment.read");

        var statuses = new List<HttpStatusCode>();
        for (var index = 0; index < 3; index++)
        {
            using var request = limitedFactory.Request(HttpMethod.Get, "/api/v1/equipment", token);
            using var response = await limitedClient.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests],
            statuses);

        using var health = await limitedClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
