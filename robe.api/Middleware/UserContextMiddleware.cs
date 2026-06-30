using Robe.Core.Interfaces;

namespace Robe.Api.Middleware;

// Runs after UseAuthentication/UseAuthorization so claims are populated.
// Stamps the correlation context with the caller's user id so every
// log/metric/alert emitted while handling this request — including from
// the Observable* decorators — can be tied back to who made the call.
// Stays empty for anonymous/unauthenticated requests.
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICorrelationContextAccessor correlation, ICurrentUser currentUser)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                correlation.UserId = currentUser.UserId;
            }
            catch (InvalidOperationException)
            {
                // ICurrentUser.UserId throws if the expected claims are missing even
                // though IsAuthenticated is true — shouldn't happen, but observability
                // plumbing must never be why a request fails.
            }
        }

        await _next(context);
    }
}
