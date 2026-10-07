using System.Net;
using System.Text.Json;
using Xunit;

namespace UserPantry.Tests.Api;

public sealed class PantryItemsOpenApiTests(
    CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task ExpiringEndpoint_DescribesContract()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(document);
        var path = json.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/pantry-items/expiring")
            .GetProperty("get");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("days", path.GetProperty("parameters").ToString());
        Assert.Contains("page", path.GetProperty("parameters").ToString());
        Assert.Contains("pageSize", path.GetProperty("parameters").ToString());
        Assert.True(path.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(path.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(path.GetProperty("responses").TryGetProperty("401", out _));
    }
}
