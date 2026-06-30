using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Observability.Azure;

public class ApplicationInsightsMetricsService : IMetricsService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ICorrelationContextAccessor _correlation;

    public ApplicationInsightsMetricsService(TelemetryClient telemetryClient, ICorrelationContextAccessor correlation)
    {
        _telemetryClient = telemetryClient;
        _correlation = correlation;
    }

    // Counters and gauges/durations both map onto Application Insights' generic
    // TrackMetric — Azure Monitor distinguishes them by name/aggregation, not type.
    public void Increment(string name, double value = 1, IReadOnlyDictionary<string, string>? tags = null) =>
        Track(name, value, tags);

    public void RecordValue(string name, double value, IReadOnlyDictionary<string, string>? tags = null) =>
        Track(name, value, tags);

    private void Track(string name, double value, IReadOnlyDictionary<string, string>? tags)
    {
        var telemetry = new MetricTelemetry(name, value);
        telemetry.Properties["correlationId"] = _correlation.CorrelationId;
        if (tags is not null)
            foreach (var (key, value2) in tags)
                telemetry.Properties[key] = value2;

        _telemetryClient.TrackMetric(telemetry);
    }
}
