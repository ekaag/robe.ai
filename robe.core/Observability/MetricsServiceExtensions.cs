using System.Diagnostics;
using Robe.Core.Interfaces;

namespace Robe.Core.Observability;

public static class MetricsServiceExtensions
{
    // Records elapsed milliseconds as a value metric when the returned scope is disposed.
    public static IDisposable Time(this IMetricsService metrics, string name, IReadOnlyDictionary<string, string>? tags = null) =>
        new TimerScope(metrics, name, tags);

    private sealed class TimerScope : IDisposable
    {
        private readonly IMetricsService _metrics;
        private readonly string _name;
        private readonly IReadOnlyDictionary<string, string>? _tags;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public TimerScope(IMetricsService metrics, string name, IReadOnlyDictionary<string, string>? tags)
        {
            _metrics = metrics;
            _name = name;
            _tags = tags;
        }

        public void Dispose() => _metrics.RecordValue(_name, _stopwatch.Elapsed.TotalMilliseconds, _tags);
    }
}
