namespace Robe.Core.Interfaces;

public interface IMetricsService
{
    void Increment(string name, double value = 1, IReadOnlyDictionary<string, string>? tags = null);

    void RecordValue(string name, double value, IReadOnlyDictionary<string, string>? tags = null);
}
