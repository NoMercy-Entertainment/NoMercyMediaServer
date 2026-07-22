// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Streaming.Dtos;

public record StartLiveSessionResponse(
    [property: JsonProperty(propertyName: "session_id")] string SessionId,
    [property: JsonProperty(propertyName: "playlist_url")] string PlaylistUrl,
    [property: JsonProperty(propertyName: "quality_id")] string QualityId,
    [property: JsonProperty(propertyName: "quality_label")] string QualityLabel
)
{
    /// <summary>
    /// Details of the quality variant selected for this session.
    /// </summary>
    [JsonProperty(propertyName: "selected_variant")]
    public SelectedVariantDto? SelectedVariant { get; init; }

    /// <summary>
    /// UTC timestamp at which this session will be reaped if idle.
    /// </summary>
    [JsonProperty(propertyName: "expires_at")]
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Indicates how the client should consume this response.
    /// "live" — consume playlist_url via HLS (default, always present for transcode/remux).
    /// "direct" — the file can be played directly; use direct_stream_url instead of playlist_url.
    /// </summary>
    [JsonProperty(propertyName: "mode")]
    public string Mode { get; init; } = "live";

    /// <summary>
    /// Populated when Mode is "direct". The URL served by DynamicStaticFilesMiddleware
    /// at /{hostFolder}/{filename} that the client can feed straight into its player.
    /// Null for live/transcode sessions.
    /// </summary>
    [JsonProperty(propertyName: "direct_stream_url")]
    public string? DirectStreamUrl { get; init; }

    /// <summary>
    /// When Mode is "direct", explains why direct play was chosen.
    /// Null for live/transcode sessions.
    /// </summary>
    [JsonProperty(propertyName: "direct_play_reason")]
    public string? DirectPlayReason { get; init; }
}

/// <summary>
/// Codec + resolution + bitrate of the quality variant chosen for a live session.
/// </summary>
public record SelectedVariantDto(
    [property: JsonProperty(propertyName: "codec")] string Codec,
    [property: JsonProperty(propertyName: "width")] int Width,
    [property: JsonProperty(propertyName: "height")] int Height,
    [property: JsonProperty(propertyName: "bitrate_kbps")] int BitrateKbps
);

public record ReportPositionRequest([property: JsonProperty(propertyName: "time_seconds")] double TimeSeconds);

/// <summary>
/// Response body returned by the position report endpoint.
/// </summary>
public record ReportPositionResponse(
    [property: JsonProperty(propertyName: "position_seconds")] double PositionSeconds,
    [property: JsonProperty(propertyName: "is_paused")] bool IsPaused
);

/// <summary>
/// Request body for the client network-health report endpoint (REST fallback
/// for clients that don't use the <c>LiveTranscodeHub.ReportBufferHealth</c>
/// SignalR method). Reports the client's download-buffer depth and its
/// measured/estimated downlink so the buffer-adaptive sweep's network axis
/// can react to network conditions independently of encoder-lead.
/// </summary>
public record ReportBufferHealthRequest(
    [property: JsonProperty(propertyName: "buffered_seconds")] double BufferedSeconds,
    [property: JsonProperty(propertyName: "observed_bandwidth_kbps")] double ObservedBandwidthKbps
);

/// <summary>
/// Response body returned by the buffer-health report endpoint. Echoes the
/// clamped (non-negative) values the server actually recorded.
/// </summary>
public record ReportBufferHealthResponse(
    [property: JsonProperty(propertyName: "buffered_seconds")] double BufferedSeconds,
    [property: JsonProperty(propertyName: "observed_bandwidth_kbps")] double ObservedBandwidthKbps
);

/// <summary>
/// Request body for the in-session quality change endpoint.
/// </summary>
public record ChangeQualityRequest([property: JsonProperty(propertyName: "quality_id")] string QualityId);

/// <summary>
/// Response body returned after a successful quality change.
/// </summary>
public record ChangeQualityResponse(
    [property: JsonProperty(propertyName: "quality_id")] string QualityId,
    [property: JsonProperty(propertyName: "quality_label")] string QualityLabel
);

/// <summary>
/// Request body for the in-session seek endpoint.
/// </summary>
public record SeekRequest([property: JsonProperty(propertyName: "position_seconds")] double PositionSeconds);

/// <summary>
/// Response body returned after a successful seek.
/// </summary>
public record SeekResponse([property: JsonProperty(propertyName: "position_seconds")] double PositionSeconds);

/// <summary>
/// Admin-safe view of a single live session returned by GET /sessions.
/// Does not include internal cancellation tokens or scratch paths.
/// </summary>
public record LiveSessionDto(
    [property: JsonProperty(propertyName: "session_id")] string SessionId,
    [property: JsonProperty(propertyName: "state")] string State,
    [property: JsonProperty(propertyName: "quality_id")] string QualityId,
    [property: JsonProperty(propertyName: "quality_label")] string QualityLabel,
    [property: JsonProperty(propertyName: "width")] int Width,
    [property: JsonProperty(propertyName: "height")] int Height,
    [property: JsonProperty(propertyName: "bitrate_kbps")] int BitrateKbps,
    [property: JsonProperty(propertyName: "position_seconds")] double PositionSeconds,
    [property: JsonProperty(propertyName: "buffer_ahead_seconds")] double BufferAheadSeconds,
    [property: JsonProperty(propertyName: "segment_count")] int SegmentCount,
    [property: JsonProperty(propertyName: "is_complete")] bool IsComplete,
    [property: JsonProperty(propertyName: "last_access")] DateTime LastAccess
);
