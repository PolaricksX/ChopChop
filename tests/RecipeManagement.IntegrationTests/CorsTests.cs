using System.Net;
using RecipeManagement.IntegrationTests.Support;

namespace RecipeManagement.IntegrationTests;

public sealed class CorsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task Allowed_origin_preflight_succeeds_and_returns_allow_origin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/recipes");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-correlation-id");

        using var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Unlisted_origin_preflight_does_not_return_allow_origin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/recipes");
        request.Headers.Add("Origin", "https://unlisted.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-correlation-id");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
