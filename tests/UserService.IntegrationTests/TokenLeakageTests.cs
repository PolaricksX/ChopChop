using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using UserService.Api.Contracts;
using UserService.IntegrationTests.Support;

namespace UserService.IntegrationTests;

[Collection("Serilog static logger")]
public class TokenLeakageTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Users = "/api/v1/users";
    private readonly HttpClient client = factory.CreateClient();

    private static readonly byte[] Png = EncodePng();

    private static byte[] EncodePng()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    [Fact]
    public void Sanity_logger_from_services_writes_to_captured_log_sink()
    {
        factory.Logs.Clear();
        var marker = $"token-leakage-sanity-{Guid.NewGuid():N}";

        var logger = factory.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("{Marker}", marker);

        var events = factory.Logs.Snapshot();
        Assert.NotEmpty(events);
        Assert.Contains(events, e =>
            e.RenderMessage().Contains(marker, StringComparison.Ordinal)
            || e.Properties.Values.Any(v => v.ToString().Contains(marker, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Successful_authenticated_profile_flow_does_not_log_raw_bearer_token()
    {
        factory.Logs.Clear();
        var token = factory.Jwt.Create(Guid.NewGuid().ToString());
        var body = new { username = "u" + Guid.NewGuid().ToString("N")[..12], email = $"{Guid.NewGuid():N}@example.com" };

        using var create = factory.Request(HttpMethod.Post, Users, token, body, Guid.NewGuid().ToString());
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var getMe = factory.Request(HttpMethod.Get, $"{Users}/me", token);
        using var meResponse = await client.SendAsync(getMe);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        AssertTokenWasNotLogged(token, minimumEvents: 2);
    }

    [Fact]
    public async Task Invalid_signature_token_on_get_me_does_not_log_raw_bearer_token()
    {
        factory.Logs.Clear();
        var token = factory.Jwt.Create(Guid.NewGuid().ToString(), signWithUnknownKey: true);

        using var request = factory.Request(HttpMethod.Get, $"{Users}/me", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertTokenWasNotLogged(token);
    }

    [Fact]
    public async Task Expired_token_on_get_me_does_not_log_raw_bearer_token()
    {
        factory.Logs.Clear();
        var token = factory.Jwt.Create(Guid.NewGuid().ToString(), lifetime: TimeSpan.FromMinutes(-10));

        using var request = factory.Request(HttpMethod.Get, $"{Users}/me", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertTokenWasNotLogged(token);
    }

    [Fact]
    public async Task Query_string_access_token_on_get_me_does_not_log_raw_bearer_token()
    {
        factory.Logs.Clear();
        var token = factory.Jwt.Create(Guid.NewGuid().ToString());

        using var response = await client.GetAsync($"{Users}/me?access_token={token}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertTokenWasNotLogged(token);
    }

    [Fact]
    public async Task Authenticated_profile_picture_upload_and_get_do_not_log_raw_bearer_token()
    {
        factory.Logs.Clear();
        var token = factory.Jwt.Create(Guid.NewGuid().ToString());
        var body = new { username = "u" + Guid.NewGuid().ToString("N")[..12], email = $"{Guid.NewGuid():N}@example.com" };

        using var create = factory.Request(HttpMethod.Post, Users, token, body, Guid.NewGuid().ToString());
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<UserResponse>())!;

        using var upload = factory.Request(HttpMethod.Put, $"{Users}/{created.Id}/profile-picture", token);
        using (var form = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "picture.png");
            upload.Content = form;

            using var uploadResponse = await client.SendAsync(upload);
            Assert.Equal(HttpStatusCode.NoContent, uploadResponse.StatusCode);
        }

        using var getPicture = factory.Request(HttpMethod.Get, $"{Users}/{created.Id}/profile-picture", token);
        using var getPictureResponse = await client.SendAsync(getPicture);
        Assert.Equal(HttpStatusCode.OK, getPictureResponse.StatusCode);

        AssertTokenWasNotLogged(token, minimumEvents: 3);
    }

    private void AssertTokenWasNotLogged(string token, int minimumEvents = 1)
    {
        var events = factory.Logs.Snapshot();
        Assert.True(events.Count >= minimumEvents, $"Expected at least {minimumEvents} log events but captured {events.Count}.");

        foreach (var logEvent in events)
        {
            var rendered = logEvent.RenderMessage();
            Assert.False(rendered.Contains(token, StringComparison.Ordinal));
            Assert.False(rendered.Contains("Bearer ", StringComparison.Ordinal));

            foreach (var value in logEvent.Properties.Values)
            {
                var propertyText = value.ToString();
                Assert.False(propertyText.Contains(token, StringComparison.Ordinal));
                Assert.False(propertyText.Contains("Bearer ", StringComparison.Ordinal));
            }

            var exceptionText = logEvent.Exception?.ToString();
            if (exceptionText is not null)
            {
                Assert.False(exceptionText.Contains(token, StringComparison.Ordinal));
                Assert.False(exceptionText.Contains("Bearer ", StringComparison.Ordinal));
            }
        }
    }
}
