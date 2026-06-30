namespace Robe.Core.Interfaces;

public interface ICorrelationContextAccessor
{
    string CorrelationId { get; set; }
}
