using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace UserService.IntegrationTests.Support;

/// <summary>Issues test tokens signed with a test-only RSA key. The API still validates them with the real JWT bearer handler.</summary>
public sealed class TestJwt : IDisposable
{
    public const string Issuer = "https://issuer.test/realms/chopchop";
    public const string Audience = "user-service-tests";

    private readonly RSA rsa = RSA.Create(2048);
    private readonly RSA otherRsa = RSA.Create(2048);

    public SecurityKey SigningKey => new RsaSecurityKey(rsa) { KeyId = "test-key" };

    /// <param name="lifetime">Time until expiry; a negative value produces an already-expired token.</param>
    public string Create(
        string subject,
        string? scope = "users.read users.write",
        string issuer = Issuer,
        string audience = Audience,
        TimeSpan? lifetime = null,
        bool signWithUnknownKey = false)
    {
        var key = new RsaSecurityKey(signWithUnknownKey ? otherRsa : rsa) { KeyId = "test-key" };
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5));
        var claims = new List<Claim> { new("sub", subject) };
        if (scope is not null)
        {
            claims.Add(new Claim("scope", scope));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = expires.AddHours(-1),
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public void Dispose()
    {
        rsa.Dispose();
        otherRsa.Dispose();
    }
}
