using Newtonsoft.Json;

namespace NoMercy.Encoder.Live;

/// <summary>
///     What a player can actually decode. Sent on session creation so the encoder can pick a
///     variant the client can play instead of asking it to handle the source as-is. Maps to the
///     JSON shape documented in <c>The Effortless Encoder</c> part 08, "Playing what your device
///     cannot decode".
/// </summary>
public sealed record ClientCapabilities
{
    /// <summary>Lowercase codec ids the client decodes (e.g. <c>"h264"</c>, <c>"vp9"</c>, <c>"av1"</c>).</summary>
    [JsonProperty("supported_video_codecs")]
    public IReadOnlyList<string> SupportedVideoCodecs { get; init; } = [];

    /// <summary>Lowercase audio codec ids (e.g. <c>"aac"</c>, <c>"opus"</c>, <c>"ac3"</c>).</summary>
    [JsonProperty("supported_audio_codecs")]
    public IReadOnlyList<string> SupportedAudioCodecs { get; init; } = [];

    [JsonProperty("max_width")]
    public int MaxWidth { get; init; }

    [JsonProperty("max_height")]
    public int MaxHeight { get; init; }

    /// <summary><c>"none"</c>, <c>"hdr10"</c>, <c>"hlg"</c>, <c>"dovi"</c>.</summary>
    [JsonProperty("hdr_support")]
    public string HdrSupport { get; init; } = "none";

    [JsonProperty("supports_hls_aes128")]
    public bool SupportsHlsAes128 { get; init; }
}
