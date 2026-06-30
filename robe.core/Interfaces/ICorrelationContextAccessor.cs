namespace Robe.Core.Interfaces;

public interface ICorrelationContextAccessor
{
    string CorrelationId { get; set; }

    // Authenticated caller for the current request, if any. Empty until
    // UserContextMiddleware sets it post-authentication; stays empty for
    // unauthenticated requests.
    string UserId { get; set; }
}
