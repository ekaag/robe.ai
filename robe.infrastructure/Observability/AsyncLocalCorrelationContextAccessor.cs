using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Observability;

// Singleton service; CorrelationId/UserId are backed by AsyncLocal so they
// still flow per-request/per-async-chain even though this instance is shared.
public class AsyncLocalCorrelationContextAccessor : ICorrelationContextAccessor
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();
    private static readonly AsyncLocal<string?> CurrentUserId = new();

    public string CorrelationId
    {
        get => CurrentCorrelationId.Value ?? string.Empty;
        set => CurrentCorrelationId.Value = value;
    }

    public string UserId
    {
        get => CurrentUserId.Value ?? string.Empty;
        set => CurrentUserId.Value = value;
    }
}
