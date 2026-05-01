using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Orchestration;

/// <summary>
/// Top-level entry point for an encode job. Resolves the strategy matching
/// the request's format + encode mode and hands the encode off to it.
/// This is what queue jobs call instead of <see cref="IEncoder"/> directly —
/// keeps dispatch logic out of the job class.
/// </summary>
public interface IEncodingOrchestrator
{
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    );
}
