using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace RecipeManagement.IntegrationTests.Support;

public sealed class TestJwt : IDisposable
{
    public const string Issuer = "https://issuer.test/realms/chopchop";
    public const string Audience = "recipe-management-tests";

    private readonly RSA rsa = RSA.Create(2048);
    private readonly RSA otherRsa = RSA.Create(2048);

    public SecurityKey SigningKey => new RsaSecurityKey(rsa) { KeyId = "recipe-test-key" };

    public string Create(string subject, string? scope = "recipes.read recipes.write recipes.publish",
        string issuer = Issuer, string audience = Audience, TimeSpan? lifetime = null, bool signWithUnknownKey = false)
    {
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5));
        var key = new RsaSecurityKey(signWithUnknownKey ? otherRsa : rsa) { KeyId = "recipe-test-key" };
        var claims = new List<Claim> { new("sub", subject) };
        if (scope is not null) claims.Add(new Claim("scope", scope));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = expires.AddHours(-1),
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
    }

    public void Dispose()
    {
        rsa.Dispose();
        otherRsa.Dispose();
    }
}
