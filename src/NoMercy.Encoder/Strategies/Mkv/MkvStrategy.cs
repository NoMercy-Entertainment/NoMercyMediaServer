namespace NoMercy.Encoder.Strategies.Mkv;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

/// <summary>
/// Matroska single-file output. Delegates to the shared pipeline — the
/// <see cref="NoMercy.Encoder.Output.MkvOutputStrategy"/> already emits the
/// correct FFmpeg arguments for a single .mkv output with embedded audio +
/// subtitles + chapters.
///
/// MKV does not distinguish single-pass vs two-pass conceptually (the output
/// is one file either way), but 2-pass can still matter for software
/// encoders targeting a specific bitrate. That's a future strategy.
/// </summary>
public class MkvStrategy(IEncoder encoder) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.Mkv;
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    ) => encoder.EncodeAsync(request, progress, ct);
}
