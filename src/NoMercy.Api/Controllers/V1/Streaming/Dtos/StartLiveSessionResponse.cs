using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Streaming.Dtos;

public record StartLiveSessionResponse(
    [property: JsonProperty("session_id")] string SessionId,
    [property: JsonProperty("playlist_url")] string PlaylistUrl,
    [property: JsonProperty("quality_id")] string QualityId,
    [property: JsonProperty("quality_label")] string QualityLabel
)
{
    /// <summary>
    /// Details of the quality variant selected for this session.
    /// </summary>
    [JsonProperty("selected_variant")]
    public SelectedVariantDto? SelectedVariant { get; init; }

    /// <summary>
    /// UTC timestamp at which this session will be reaped if idle.
    /// </summary>
    [JsonProperty("expires_at")]
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Codec + resolution + bitrate of the quality variant chosen for a live session.
/// </summary>
public record SelectedVariantDto(
    [property: JsonProperty("codec")] string Codec,
    [property: JsonProperty("width")] int Width,
    [property: JsonProperty("height")] int Height,
    [property: JsonProperty("bitrate_kbps")] int BitrateKbps
);

public record ReportPositionRequest([property: JsonProperty("time_seconds")] double TimeSeconds);

/// <summary>
/// Response body returned by the position report endpoint.
/// </summary>
public record ReportPositionResponse(
    [property: JsonProperty("position_seconds")] double PositionSeconds,
    [property: JsonProperty("is_paused")] bool IsPaused
);
