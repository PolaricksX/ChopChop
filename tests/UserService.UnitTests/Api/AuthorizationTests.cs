using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UserService.Api.Infrastructure;
using UserService.Api.Services;

namespace UserService.UnitTests.Api;

public class AuthorizationTests
{
    private static ClaimsPrincipal User(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void Scope_is_matched_exactly_within_a_space_delimited_claim()
    {
        var user = User(new Claim("scope", "openid users.read profile"));

        Assert.True(ScopeClaims.HasScope(user, "users.read"));
        Assert.False(ScopeClaims.HasScope(user, "users.write"));
        Assert.False(ScopeClaims.HasScope(user, "users"));
    }

    [Fact]
    public void Scp_claim_is_supported() =>
        Assert.True(ScopeClaims.HasScope(User(new Claim("scp", "users.write")), "users.write"));

    [Fact]
    public void No_scope_claim_grants_nothing() =>
        Assert.False(ScopeClaims.HasScope(User(new Claim("sub", "abc")), "users.read"));

    [Fact]
    public void Current_user_comes_from_the_validated_sub_claim()
    {
        var context = new DefaultHttpContext { User = User(new Claim("sub", "sub-123")) };

        Assert.Equal("sub-123", new CurrentUser(new HttpContextAccessor { HttpContext = context }).Id);
    }

    [Fact]
    public void Current_user_without_sub_fails() =>
        Assert.Throws<InvalidOperationException>(() => new CurrentUser(new HttpContextAccessor { HttpContext = new DefaultHttpContext() }).Id);

    [Theory]
    [InlineData("https://idp.example.com/realms/x", "user-service", true)]
    [InlineData("", "user-service", false)]
    [InlineData("not a uri", "user-service", false)]
    [InlineData("https://idp.example.com", "", false)]
    public void Auth_options_require_authority_and_audience(string authority, string audience, bool expected) =>
        Assert.Equal(expected, AuthOptions.IsValid(new AuthOptions { Authority = authority, Audience = audience }));
}
