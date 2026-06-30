using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Azure;

public class AzureApplicationInsightsLogService : ILogService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ICorrelationContextAccessor _correlation;

    public AzureApplicationInsightsLogService(TelemetryClient telemetryClient, ICorrelationContextAccessor correlation)
    {
        _telemetryClient = telemetryClient;
        _correlation = correlation;
    }

    public void Log(
        LogSeverity severity,
        string message,
        IReadOnlyDictionary<string, object?>? properties = null,
        Exception? exception = null)
    {
        var severityLevel = ToSeverityLevel(severity);

        if (exception is not null)
        {
            var exceptionTelemetry = new ExceptionTelemetry(exception)
            {
                SeverityLevel = severityLevel,
                Message = message
            };
            Populate(exceptionTelemetry.Properties, properties);
            _telemetryClient.TrackException(exceptionTelemetry);
            return;
        }

        var traceTelemetry = new TraceTelemetry(message, severityLevel);
        Populate(traceTelemetry.Properties, properties);
        _telemetryClient.TrackTrace(traceTelemetry);
    }

    private void Populate(IDictionary<string, string> target, IReadOnlyDictionary<string, object?>? properties)
    {
        target["correlationId"] = _correlation.CorrelationId;
        target["userId"] = _correlation.UserId;
        if (properties is null) return;
        foreach (var (key, value) in properties)
            target[key] = value?.ToString() ?? string.Empty;
    }

    private static SeverityLevel ToSeverityLevel(LogSeverity severity) => severity switch
    {
        LogSeverity.Trace => SeverityLevel.Verbose,
        LogSeverity.Debug => SeverityLevel.Verbose,
        LogSeverity.Information => SeverityLevel.Information,
        LogSeverity.Warning => SeverityLevel.Warning,
        LogSeverity.Error => SeverityLevel.Error,
        LogSeverity.Critical => SeverityLevel.Critical,
        _ => SeverityLevel.Information
    };
}
