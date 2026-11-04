using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RecipeManagement.IntegrationTests.Support;

namespace RecipeManagement.IntegrationTests;

public sealed class RecipeEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Recipes = "/api/v1/recipes";
    private readonly HttpClient client = factory.CreateClient();

    private sealed record Caller(string Id, string Token);

    private Caller NewCaller() { var id = Guid.NewGuid().ToString(); return new(id, factory.Jwt.Create(id)); }

    private static object Body(string title = "Tomato soup") => new
    {
        title,
        description = "A simple recipe",
        servings = 2,
        ingredients = new[] { new { name = "Tomato", quantity = 2m, unit = "piece" } },
        steps = new[] { new { order = 1, content = "Cook the tomato." } },
        allergenStatus = "Declared",
        allergens = Array.Empty<string>(),
        tags = new[] { "quick" }
    };

    private async Task<HttpResponseMessage> SendAsync(Caller? caller, HttpMethod method, string url, object? body = null)
    {
        using var request = factory.Request(method, url, caller?.Token, body);
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("excludedAllergens=NotAnAllergen")]
    [InlineData("sort=unsupported")]
    [InlineData("tags=tag1,tag2,tag3,tag4,tag5,tag6,tag7,tag8,tag9,tag10,tag11,tag12,tag13,tag14,tag15,tag16,tag17,tag18,tag19,tag20,tag21,tag22,tag23,tag24,tag25,tag26,tag27,tag28,tag29,tag30,tag31,tag32,tag33,tag34,tag35,tag36,tag37,tag38,tag39,tag40,tag41,tag42,tag43,tag44,tag45,tag46,tag47,tag48,tag49,tag50,tag51")]
    [InlineData("text=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Invalid_public_search_parameters_return_problem_details(string query)
    {
        using var response = await client.GetAsync($"{Recipes}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Validation failed", problem.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Public_list_and_detail_are_anonymous_and_have_public_shapes()
    {
        var caller = NewCaller();
        using var create = await SendAsync(caller, HttpMethod.Post, Recipes, Body());
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("recipeId").GetGuid();
        using var publish = await SendAsync(caller, HttpMethod.Post, $"{Recipes}/{id}/publish");
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        using var list = await client.GetAsync(Recipes);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(page.TryGetProperty("items", out _));
        Assert.True(page.TryGetProperty("page", out _));
        using var detail = await client.GetAsync($"{Recipes}/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(1, (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Create_validate_isolate_update_publish_unpublish_hide_and_delete_draft()
    {
        var owner = NewCaller();
        using var invalid = await SendAsync(owner, HttpMethod.Post, Recipes, Body(title: ""));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var create = await SendAsync(owner, HttpMethod.Post, Recipes, Body());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("recipeId").GetGuid();
        Assert.Equal(1, created.GetProperty("version").GetProperty("version").GetInt32());

        var other = NewCaller();
        using var forbiddenDraft = await SendAsync(other, HttpMethod.Get, $"{Recipes}/{id}/draft");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenDraft.StatusCode);

        using var update = await SendAsync(owner, HttpMethod.Put, $"{Recipes}/{id}/draft", new { expectedVersion = 1, content = Body("Updated soup") });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        using var publish = await SendAsync(owner, HttpMethod.Post, $"{Recipes}/{id}/publish");
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        using var unpublish = await SendAsync(owner, HttpMethod.Post, $"{Recipes}/{id}/unpublish");
        Assert.Equal(HttpStatusCode.NoContent, unpublish.StatusCode);
        using var hidden = await client.GetAsync($"{Recipes}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        using var deleteCreate = await SendAsync(owner, HttpMethod.Post, Recipes, Body("Delete me"));
        var deleteId = (await deleteCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recipeId").GetGuid();
        using var delete = await SendAsync(owner, HttpMethod.Delete, $"{Recipes}/{deleteId}/draft");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var missing = await SendAsync(owner, HttpMethod.Get, $"{Recipes}/{deleteId}/draft");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
