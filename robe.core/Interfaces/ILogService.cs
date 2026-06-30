using Robe.Core.Observability;

namespace Robe.Core.Interfaces;

public interface ILogService
{
    void Log(
        LogSeverity severity,
        string message,
        IReadOnlyDictionary<string, object?>? properties = null,
        Exception? exception = null);
}
