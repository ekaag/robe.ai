using Robe.Core.Observability;

namespace Robe.Core.Interfaces;

public interface IAlertService
{
    Task RaiseAsync(
        AlertSeverity severity,
        string message,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken ct = default);
}
