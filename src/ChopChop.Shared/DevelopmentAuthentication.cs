using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChopChop.Shared;

public static class DevelopmentAuthentication
{
    public const string ConfigurationKey = "Auth:DevelopmentMode";
    public const string SchemeName = "ChopChopDevelopment";
    public const string UserId = "00000000-0000-0000-0000-000000000001";

    public static bool AddIfEnabled(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var setting = configuration[ConfigurationKey];
        if (setting is null || !bool.TryParse(setting, out var enabled))
        {
            if (setting is null)
            {
                return false;
            }

            throw new InvalidOperationException($"{ConfigurationKey} must be a Boolean value.");
        }

        if (!enabled)
        {
            return false;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException($"{ConfigurationKey} can only be enabled in the Development environment.");
        }

        services.AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(SchemeName, _ => { });
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, DevelopmentAuthorizationResultHandler>();
        return true;
    }

    internal static ClaimsPrincipal CreatePrincipal() =>
        new(new ClaimsIdentity(
        [
            new Claim("sub", UserId),
            new Claim("iss", "chopchop-development"),
        ], SchemeName, "name", "role"));
}

public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ticket = new AuthenticationTicket(DevelopmentAuthentication.CreatePrincipal(), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class DevelopmentAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallbackHandler = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult) =>
        authorizeResult.Succeeded
            ? fallbackHandler.HandleAsync(next, context, policy, authorizeResult)
            : next(context);
}
