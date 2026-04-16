namespace NoMercy.Encoder.Strategies.Hls;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

/// <summary>
/// HLS single-pass strategy. Currently delegates to the shared 6-stage
/// <see cref="IEncoder"/> pipeline — the pipeline already produces a correct
/// HLS single-pass encode. Future strategies (2-pass HLS, MKV, DASH, live)
/// can either delegate similarly or own their own sub-stages while reusing
/// the injected building blocks.
///
/// This thin wrapper exists so the orchestrator can dispatch by
/// {format, encode mode} today, and we can peel format-specific logic out
/// of the shared stages into per-strategy code later without breaking the
/// queue jobs.
/// </summary>
public class HlsSinglePassStrategy(IEncoder encoder) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.Hls;
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    ) => encoder.EncodeAsync(request, progress, ct);
}
