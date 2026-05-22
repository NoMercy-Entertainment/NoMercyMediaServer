using Newtonsoft.Json;

namespace NoMercy.Encoder.Live;

/// <summary>
///     The variant the live transcode will produce: codec ids the client decodes, dimensions
///     clamped to the client's max, and a sensible bitrate target for the resolution + codec.
/// </summary>
public sealed record SelectedVariant
{
    [JsonProperty("codec")]
    public string Codec { get; init; } = string.Empty;

    [JsonProperty("audio_codec")]
    public string AudioCodec { get; init; } = string.Empty;

    [JsonProperty("width")]
    public int Width { get; init; }

    [JsonProperty("height")]
    public int Height { get; init; }

    [JsonProperty("bitrate_kbps")]
    public int BitrateKbps { get; init; }

    [JsonProperty("convert_hdr_to_sdr")]
    public bool ConvertHdrToSdr { get; init; }
}
