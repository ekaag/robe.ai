using Robe.Core.Interfaces;

namespace Robe.Core.Observability;

public static class LogServiceExtensions
{
    public static void Trace(this ILogService log, string message, IReadOnlyDictionary<string, object?>? properties = null) =>
        log.Log(LogSeverity.Trace, message, properties);

    public static void Debug(this ILogService log, string message, IReadOnlyDictionary<string, object?>? properties = null) =>
        log.Log(LogSeverity.Debug, message, properties);

    public static void Info(this ILogService log, string message, IReadOnlyDictionary<string, object?>? properties = null) =>
        log.Log(LogSeverity.Information, message, properties);

    public static void Warn(this ILogService log, string message, IReadOnlyDictionary<string, object?>? properties = null) =>
        log.Log(LogSeverity.Warning, message, properties);

    public static void Error(this ILogService log, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        log.Log(LogSeverity.Error, message, properties, exception);

    public static void Critical(this ILogService log, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        log.Log(LogSeverity.Critical, message, properties, exception);
}
