using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Robe.Infrastructure.Auth;

/// <summary>
/// Header-based auth for local dev and tests. Replace with JWT bearer for production.
/// Send X-User-Id: &lt;userId&gt; to authenticate as a specific user.
/// Omit the header to authenticate as the default "dev-user" identity.
/// </summary>
public class LocalAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName      = "Local";
    public const string UserIdHeader    = "X-User-Id";
    public const string UserNameHeader  = "X-User-Name";
    public const string ProviderHeader  = "X-User-Provider";

    public LocalAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock) { }

    public const string DefaultDevUserId = "dev-user";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.TryGetValue(UserIdHeader, out var userIdValues) &&
                     !string.IsNullOrWhiteSpace(userIdValues.ToString())
            ? userIdValues.ToString()
            : DefaultDevUserId;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };

        if (Request.Headers.TryGetValue(UserNameHeader, out var nameValues) &&
            !string.IsNullOrWhiteSpace(nameValues.ToString()))
            claims.Add(new Claim(ClaimTypes.Name, nameValues.ToString()));

        if (Request.Headers.TryGetValue(ProviderHeader, out var providerValues) &&
            !string.IsNullOrWhiteSpace(providerValues.ToString()))
            claims.Add(new Claim("idp", providerValues.ToString()));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket   = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
