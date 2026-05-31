using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Strategies.Audio;

/// <summary>
/// Audio-only HLS strategy. Encodes audio input to AAC segments and an
/// <c>audio.m3u8</c> VOD playlist. No video, no subtitles, no master
/// playlist — the music player (hls.js) loads the variant playlist directly.
/// </summary>
public class AudioHlsStrategy(IEncoder encoder) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.AudioHls;
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    ) => encoder.EncodeAsync(request, progress, ct);
}
