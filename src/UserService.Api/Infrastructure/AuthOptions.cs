using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace UserService.Api.Infrastructure;

public sealed class AuthOptions
{
    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;

    public string[] ValidAlgorithms { get; set; } = ["RS256", "ES256"];

    public static bool IsValid([NotNullWhen(true)] AuthOptions? options) =>
        options is not null
        && Uri.TryCreate(options.Authority, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(options.Audience)
        && options.ValidAlgorithms.Length > 0;
}

/// <summary>Configures the framework JWT bearer handler from <c>Auth:*</c>. No custom token validation.</summary>
internal sealed class ConfigureJwtBearer(IOptions<AuthOptions> auth) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var settings = auth.Value;
        options.Authority = settings.Authority;
        options.Audience = settings.Audience;
        options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.RequireSignedTokens = true;
        options.TokenValidationParameters.RequireExpirationTime = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidAlgorithms = settings.ValidAlgorithms;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    }

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
