using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Observability;

// Singleton service; CorrelationId is backed by AsyncLocal so it still flows
// per-request/per-async-chain even though this instance is shared.
public class AsyncLocalCorrelationContextAccessor : ICorrelationContextAccessor
{
    private static readonly AsyncLocal<string?> Current = new();

    public string CorrelationId
    {
        get => Current.Value ?? string.Empty;
        set => Current.Value = value;
    }
}
