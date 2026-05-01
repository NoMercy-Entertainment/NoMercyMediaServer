using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Strategies.Dash;

/// <summary>
/// DASH single-pass output. Delegates to the shared pipeline — the
/// <see cref="NoMercy.Encoder.Output.DashOutputStrategy"/> emits a segmented
/// DASH output with an MPD manifest. Required target for Widevine/PlayReady
/// CENC DRM in a future Phase 11 strategy.
/// </summary>
public class DashSinglePassStrategy(IEncoder encoder) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.Dash;
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    ) => encoder.EncodeAsync(request, progress, ct);
}
