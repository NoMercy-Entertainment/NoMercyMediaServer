using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;

namespace NoMercy.Api.Controllers.V1.Streaming.Dtos;

public record StartLiveSessionRequest(
    [property: JsonProperty("video_file_id")] string VideoFileId,
    [property: JsonProperty("client_caps")] ClientCapabilitiesDto ClientCaps,
    [property: JsonProperty("start_time_seconds")] double StartTimeSeconds,
    [property: JsonProperty("preferred_quality")] string? PreferredQuality
);

public record ClientCapabilitiesDto(
    [property: JsonProperty("video_codecs")] VideoCodecType[] VideoCodecs,
    [property: JsonProperty("audio_codecs")] AudioCodecType[] AudioCodecs,
    [property: JsonProperty("containers")] string[] Containers,
    [property: JsonProperty("max_width")] int MaxWidth,
    [property: JsonProperty("max_height")] int MaxHeight,
    [property: JsonProperty("supports_hdr")] bool SupportsHdr,
    [property: JsonProperty("supports_10bit")] bool Supports10Bit,
    [property: JsonProperty("max_bitrate_kbps")] int MaxBitrateKbps
);
