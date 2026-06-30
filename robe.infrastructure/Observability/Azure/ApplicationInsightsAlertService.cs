using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Azure;

// Raises an "Alert" event into Application Insights; Azure Monitor alert rules
// (configured on the workspace) watch this signal and own thresholding/routing
// to Action Groups. This service does not evaluate thresholds itself.
public class ApplicationInsightsAlertService : IAlertService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ICorrelationContextAccessor _correlation;

    public ApplicationInsightsAlertService(TelemetryClient telemetryClient, ICorrelationContextAccessor correlation)
    {
        _telemetryClient = telemetryClient;
        _correlation = correlation;
    }

    public Task RaiseAsync(
        AlertSeverity severity,
        string message,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken ct = default)
    {
        var telemetry = new EventTelemetry("Alert");
        telemetry.Properties["severity"] = severity.ToString();
        telemetry.Properties["message"] = message;
        telemetry.Properties["correlationId"] = _correlation.CorrelationId;
        if (context is not null)
            foreach (var (key, value) in context)
                telemetry.Properties[key] = value?.ToString() ?? string.Empty;

        _telemetryClient.TrackEvent(telemetry);
        return Task.CompletedTask;
    }
}
