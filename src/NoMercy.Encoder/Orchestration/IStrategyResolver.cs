using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Strategies;

namespace NoMercy.Encoder.Orchestration;

public interface IStrategyResolver
{
    /// <summary>
    /// Returns the strategy registered for <paramref name="format"/> +
    /// <paramref name="mode"/>, or <c>null</c> when no strategy is registered
    /// (e.g. 2-pass DASH before that strategy ships). Callers must handle null
    /// by returning a validation error instead of crashing.
    /// </summary>
    IEncodingStrategy? Resolve(OutputFormat format, EncodeMode mode);
}
