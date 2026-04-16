namespace NoMercy.Encoder.Strategies.Mp4;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

/// <summary>
/// MP4 single-file single-pass output. Delegates to the shared pipeline — the
/// <see cref="NoMercy.Encoder.Output.Mp4OutputStrategy"/> already emits a single
/// faststart-muxed .mp4 with video + audio. Subtitles become sidecar files
/// because MP4's in-container subtitle support is limited.
/// </summary>
public class Mp4SinglePassStrategy(IEncoder encoder) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.Mp4;
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    ) => encoder.EncodeAsync(request, progress, ct);
}
