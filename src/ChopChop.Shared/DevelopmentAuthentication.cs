using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChopChop.Shared;

public static class DevelopmentAuthentication
{
    public const string Scheme = "Development";
    public const string ConfigurationKey = "Auth:DevelopmentAuthentication";
    public const string UserId = "00000000-0000-0000-0000-0000000000d0";

    // Returns true when the fixed development user replaces JWT bearer authentication.
    public static bool TryAddDevelopmentAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>(ConfigurationKey))
            return false;

        if (!environment.IsDevelopment())
            throw new InvalidOperationException($"{ConfigurationKey} may only be enabled in the Development environment.");

        services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(Scheme, null);
        return true;
    }
}

internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Scopes =
        "recipes.read recipes.write recipes.publish cookbook.read cookbook.write users.read users.write mealplans.read mealplans.write";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", DevelopmentAuthentication.UserId),
                new Claim("preferred_username", "dev-user"),
                new Claim("scope", Scopes),
                new Claim("role", "review-moderator")
            ],
            DevelopmentAuthentication.Scheme,
            "preferred_username",
            "role");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), DevelopmentAuthentication.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
