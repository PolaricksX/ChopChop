using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Equipment.IntegrationTests.Support;

public sealed class TestJwt : IDisposable
{
    public const string Issuer = "https://issuer.test/realms/chopchop";
    public const string Audience = "equipment-api";

    private readonly RSA rsa = RSA.Create(2048);
    private readonly RSA otherRsa = RSA.Create(2048);

    public SecurityKey SigningKey => new RsaSecurityKey(rsa) { KeyId = "equipment-test-key" };

    public string Create(
        string subject,
        string? scope = "equipment.read equipment.write",
        string issuer = Issuer,
        string audience = Audience,
        TimeSpan? lifetime = null,
        bool signWithUnknownKey = false)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim> { new("sub", subject) };
        if (scope is not null)
        {
            claims.Add(new Claim("scope", scope));
        }

        var key = new RsaSecurityKey(signWithUnknownKey ? otherRsa : rsa)
        {
            KeyId = "equipment-test-key",
        };
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.AddMinutes(-1),
            Expires = now.Add(lifetime ?? TimeSpan.FromMinutes(5)),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
    }

    public void Dispose()
    {
        rsa.Dispose();
        otherRsa.Dispose();
    }
}
