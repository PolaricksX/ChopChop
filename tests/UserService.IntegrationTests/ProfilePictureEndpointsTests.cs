using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using UserService.Api.Contracts;
using UserService.Data.Context;
using UserService.Domain.Entities;
using UserService.IntegrationTests.Support;

namespace UserService.IntegrationTests;

public class ProfilePictureEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Users = "/api/v1/users";
    private readonly HttpClient client = factory.CreateClient();

    private static readonly byte[] Png = Encode(SKEncodedImageFormat.Png);
    private static readonly byte[] Jpeg = Encode(SKEncodedImageFormat.Jpeg);

    private static byte[] Encode(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private async Task<(string Token, Guid Id)> CreateUserAsync()
    {
        var token = factory.Jwt.Create(Guid.NewGuid().ToString());
        var body = new { username = "u" + Guid.NewGuid().ToString("N")[..12], email = $"{Guid.NewGuid():N}@example.com" };
        using var request = factory.Request(HttpMethod.Post, Users, token, body, Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (token, (await response.Content.ReadFromJsonAsync<UserResponse>())!.Id);
    }

    private async Task<HttpResponseMessage> UploadAsync(string token, Guid id, byte[]? bytes, string contentType = "image/png")
    {
        using var request = factory.Request(HttpMethod.Put, $"{Users}/{id}/profile-picture", token);
        var form = new MultipartFormDataContent();
        if (bytes is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "file", "picture");
        }
        else
        {
            form.Add(new StringContent("no file here"), "note");
        }

        request.Content = form;
        return await client.SendAsync(request);
    }

    private async Task<string?> StoredPictureKeyAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserServiceDbContext>();
        return await db.Set<UserProfile>().AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.ProfilePictureKey)
            .SingleAsync();
    }

    private static byte[] OfSize(byte[] header, int size)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    [Fact]
    public async Task Upload_png_returns_204_and_persists_the_picture_reference()
    {
        var (token, id) = await CreateUserAsync();

        using var response = await UploadAsync(token, id, Png);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await StoredPictureKeyAsync(id)));
    }

    [Fact]
    public async Task Upload_jpeg_returns_204()
    {
        var (token, id) = await CreateUserAsync();

        using var response = await UploadAsync(token, id, Jpeg, "image/jpeg");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Replacing_the_picture_stores_a_new_reference()
    {
        var (token, id) = await CreateUserAsync();
        using var first = await UploadAsync(token, id, Png);
        var firstKey = await StoredPictureKeyAsync(id);

        using var second = await UploadAsync(token, id, Jpeg, "image/jpeg");

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.NotEqual(firstKey, await StoredPictureKeyAsync(id));
    }

    [Fact]
    public async Task Upload_to_another_users_profile_is_404_and_changes_nothing()
    {
        var (_, ownerId) = await CreateUserAsync();
        var (otherToken, _) = await CreateUserAsync();

        using var response = await UploadAsync(otherToken, ownerId, Png);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await StoredPictureKeyAsync(ownerId));
    }

    [Fact]
    public async Task Request_without_a_file_is_400()
    {
        var (token, id) = await CreateUserAsync();

        using var response = await UploadAsync(token, id, bytes: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Empty_file_is_400()
    {
        var (token, id) = await CreateUserAsync();

        using var response = await UploadAsync(token, id, []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Corrupt_image_with_a_valid_signature_is_400()
    {
        var (token, id) = await CreateUserAsync();
        var corrupt = OfSize(Png[..8], 200);

        using var response = await UploadAsync(token, id, corrupt);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_format_is_415()
    {
        var (token, id) = await CreateUserAsync();
        byte[] gif = [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0];

        using var response = await UploadAsync(token, id, gif, "image/gif");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Declared_type_not_matching_the_file_is_415_in_both_directions()
    {
        var (token, id) = await CreateUserAsync();

        using var pngAsJpeg = await UploadAsync(token, id, Png, "image/jpeg");
        using var jpegAsPng = await UploadAsync(token, id, Jpeg, "image/png");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, pngAsJpeg.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, jpegAsPng.StatusCode);
    }

    [Fact]
    public async Task File_over_1_MB_is_413_with_problem_details()
    {
        var (token, id) = await CreateUserAsync();
        var tooLarge = OfSize(Png, 1_572_864);

        using var response = await UploadAsync(token, id, tooLarge);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Rejected_upload_keeps_the_previous_picture()
    {
        var (token, id) = await CreateUserAsync();
        using var valid = await UploadAsync(token, id, Png);
        var keyBefore = await StoredPictureKeyAsync(id);

        using var rejected = await UploadAsync(token, id, OfSize(Png[..8], 200));

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(keyBefore, await StoredPictureKeyAsync(id));
    }

    [Fact]
    public async Task Concurrent_uploads_return_204_or_409_and_never_500()
    {
        var (token, id) = await CreateUserAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => UploadAsync(token, id, Png)));

        try
        {
            Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }));
        }
        finally
        {
            foreach (var r in responses) r.Dispose();
        }
    }
}
