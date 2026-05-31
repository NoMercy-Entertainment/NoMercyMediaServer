using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Strategies.Audio;

/// <summary>
/// Ogg single-file output. Container accepts vorbis (default), opus, or flac
/// depending on profile's audio codec selection.
/// </summary>
public class OggStrategy(IEncoder encoder) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.Ogg;
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    ) => encoder.EncodeAsync(request, progress, ct);
}
