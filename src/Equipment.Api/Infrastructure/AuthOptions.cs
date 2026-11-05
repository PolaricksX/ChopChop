using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Equipment.Api.Infrastructure;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Authority { get; set; } = string.Empty;

    public string? Issuer { get; set; }

    public string Audience { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;

    public string[] ValidAlgorithms { get; set; } = ["RS256", "ES256"];

    public static bool IsValid([NotNullWhen(true)] AuthOptions? options, bool isDevelopment)
    {
        if (options is null
            || !IsValidIdentityUri(options.Authority, isDevelopment)
            || (!string.IsNullOrWhiteSpace(options.Issuer) && !IsValidIdentityUri(options.Issuer, isDevelopment))
            || string.IsNullOrWhiteSpace(options.Audience)
            || (!isDevelopment && !options.RequireHttpsMetadata)
            || options.ValidAlgorithms is not { Length: > 0 })
        {
            return false;
        }

        return options.ValidAlgorithms.All(algorithm =>
            algorithm is SecurityAlgorithms.RsaSha256 or SecurityAlgorithms.EcdsaSha256);
    }

    private static bool IsValidIdentityUri(string value, bool isDevelopment) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps
            || isDevelopment && uri.Scheme == Uri.UriSchemeHttp);
}

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
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        if (!string.IsNullOrWhiteSpace(settings.Issuer))
        {
            options.TokenValidationParameters.ValidIssuer = settings.Issuer;
        }

        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.RequireSignedTokens = true;
        options.TokenValidationParameters.RequireExpirationTime = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidAlgorithms = settings.ValidAlgorithms;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    }

    public void Configure(JwtBearerOptions options) =>
        Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
