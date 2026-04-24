namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Profile-level HLS muxer options. These are stored in the profile JSON column and
/// forwarded to the HLS output strategy at encode time.
/// </summary>
public record HlsOptions
{
    /// <summary>
    /// HLS playlist type passed to -hls_playlist_type. Use "vod" for pre-recorded content
    /// or "event" for live streams where segments are appended but never removed.
    /// Default: vod.
    /// </summary>
    public string PlaylistType { get; init; } = "vod";

    /// <summary>
    /// HLS segment container format. Use "mpegts" for broadest device compatibility or
    /// "fmp4" for low-latency and CMAF-compatible delivery.
    /// Default: mpegts.
    /// </summary>
    public string SegmentType { get; init; } = "mpegts";

    /// <summary>
    /// When true, adds the #EXT-X-INDEPENDENT-SEGMENTS tag to the playlist, indicating
    /// every segment can be decoded without reference to any prior segment.
    /// Default: true — this tag is correct and safe for all VOD HLS output and was
    /// always emitted by the strategy before HlsOptions was introduced.
    /// </summary>
    public bool IndependentSegments { get; init; } = true;
}
