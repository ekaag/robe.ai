using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Robe.Infrastructure.Auth;

/// <summary>
/// Header-based auth for local dev and tests. Replace with JWT bearer for production.
///
/// Priority order:
///   1. Authorization: Bearer &lt;jwt&gt;  — decodes payload (no sig validation) to get real user identity
///   2. X-User-Id header            — explicit override
///   3. No credentials               — AuthenticateResult.NoResult(), [Authorize] returns 401
/// </summary>
public class LocalAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName     = "Local";
    public const string UserIdHeader   = "X-User-Id";
    public const string UserNameHeader = "X-User-Name";
    public const string ProviderHeader = "X-User-Provider";

    public LocalAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Try to extract identity from a Bearer JWT (no signature check — local dev only).
        if (Request.Headers.TryGetValue("Authorization", out var authValues))
        {
            var authHeader = authValues.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader["Bearer ".Length..].Trim();
                var ticket = TryBuildTicketFromJwt(token);
                if (ticket != null)
                    return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }

        // 2. Fall back to explicit X-User-Id header. No Authorization header and no
        // X-User-Id means the caller sent no credentials at all — let [Authorize] 401.
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues) ||
            string.IsNullOrWhiteSpace(userIdValues.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValues.ToString();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };

        if (Request.Headers.TryGetValue(UserNameHeader, out var nameValues) &&
            !string.IsNullOrWhiteSpace(nameValues.ToString()))
            claims.Add(new Claim(ClaimTypes.Name, nameValues.ToString()));

        if (Request.Headers.TryGetValue(ProviderHeader, out var providerValues) &&
            !string.IsNullOrWhiteSpace(providerValues.ToString()))
            claims.Add(new Claim("idp", providerValues.ToString()));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    private AuthenticationTicket? TryBuildTicketFromJwt(string token)
    {
        try
        {
            // JWT = base64url(header).base64url(payload).signature — decode payload only.
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payloadJson = Base64UrlDecode(parts[1]);
            using var doc   = JsonDocument.Parse(payloadJson);
            var root        = doc.RootElement;

            // Prefer oid (stable Entra object ID) over sub.
            var userId = GetString(root, "oid") ?? GetString(root, "sub");
            if (string.IsNullOrEmpty(userId)) return null;

            var claimList = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };

            var name = GetString(root, "name")
                    ?? CombineNames(GetString(root, "given_name"), GetString(root, "family_name"))
                    ?? GetString(root, "email")
                    ?? GetString(root, "preferred_username");
            if (!string.IsNullOrEmpty(name))
                claimList.Add(new Claim(ClaimTypes.Name, name));

            var idp = GetString(root, "idp");
            if (!string.IsNullOrEmpty(idp))
                claimList.Add(new Claim("idp", idp));

            var identity = new ClaimsIdentity(claimList, Scheme.Name);
            return new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        }
        catch
        {
            return null; // malformed token — fall through to header/default
        }
    }

    private static string? GetString(JsonElement root, string key) =>
        root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static string? CombineNames(string? given, string? family) =>
        (given, family) switch
        {
            (not null, not null) => $"{given} {family}",
            (not null, null)     => given,
            (null, not null)     => family,
            _                   => null
        };

    private static string Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
