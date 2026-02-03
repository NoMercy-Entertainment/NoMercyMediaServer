using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Containers;

/// <summary>
/// HLS (HTTP Live Streaming) container for adaptive bitrate streaming
/// </summary>
public sealed class HlsContainer : ContainerBase, IHlsContainer
{
    public override string FormatName => "hls";
    public override string DisplayName => "HLS (HTTP Live Streaming)";
    public override string Extension => ".m3u8";
    public override string MimeType => "application/vnd.apple.mpegurl";
    public override bool SupportsStreaming => true;

    public override IReadOnlyList<string> CompatibleVideoCodecs =>
    [
        "libx264", "h264_nvenc", "h264_qsv", "h264_videotoolbox", "h264_amf",
        "libx265", "hevc_nvenc", "hevc_qsv", "hevc_videotoolbox", "hevc_amf"
    ];

    public override IReadOnlyList<string> CompatibleAudioCodecs =>
    [
        "aac", "libfdk_aac", "ac3", "eac3", "mp3"
    ];

    public override IReadOnlyList<string> CompatibleSubtitleCodecs =>
    [
        "webvtt"
    ];

    /// <summary>
    /// Segment duration in seconds (default: 4)
    /// </summary>
    public int SegmentDuration { get; set; } = 4;

    /// <summary>
    /// Playlist type (vod or event)
    /// </summary>
    public HlsPlaylistType PlaylistType { get; set; } = HlsPlaylistType.Vod;

    /// <summary>
    /// Segment filename pattern
    /// </summary>
    public string SegmentFilenamePattern { get; set; } = "segment_%05d.ts";

    /// <summary>
    /// Master playlist filename
    /// </summary>
    public string MasterPlaylistFilename { get; set; } = "playlist.m3u8";

    /// <summary>
    /// Whether to include program date time
    /// </summary>
    public bool IncludeProgramDateTime { get; set; }

    /// <summary>
    /// Whether to delete segments after concatenation
    /// </summary>
    public bool DeleteSegments { get; set; }

    /// <summary>
    /// Number of segments to keep in live playlist (0 = all)
    /// </summary>
    public int HlsListSize { get; set; }

    /// <summary>
    /// Start segment number
    /// </summary>
    public int StartNumber { get; set; }

    /// <summary>
    /// HLS flags
    /// </summary>
    public List<string> HlsFlags { get; } = ["independent_segments"];

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = [];

        // Format
        args.AddRange(["-f", "hls"]);

        // Segment duration
        args.AddRange(["-hls_time", SegmentDuration.ToString()]);

        // Playlist type
        args.AddRange(["-hls_playlist_type", PlaylistType == HlsPlaylistType.Vod ? "vod" : "event"]);

        // Segment filename
        args.AddRange(["-hls_segment_filename", SegmentFilenamePattern]);

        // HLS list size
        if (HlsListSize > 0)
        {
            args.AddRange(["-hls_list_size", HlsListSize.ToString()]);
        }
        else
        {
            args.AddRange(["-hls_list_size", "0"]);
        }

        // Start number
        if (StartNumber > 0)
        {
            args.AddRange(["-hls_start_number_source", "generic"]);
            args.AddRange(["-start_number", StartNumber.ToString()]);
        }

        // Program date time
        if (IncludeProgramDateTime)
        {
            HlsFlags.Add("program_date_time");
        }

        // Delete segments
        if (DeleteSegments)
        {
            HlsFlags.Add("delete_segments");
        }

        // Combine flags
        if (HlsFlags.Count > 0)
        {
            args.AddRange(["-hls_flags", string.Join("+", HlsFlags)]);
        }

        return args;
    }

    /// <summary>
    /// Gets the bitstream filter needed for HLS with H.264/H.265
    /// </summary>
    public static string GetBitstreamFilter(string videoCodec)
    {
        return videoCodec.ToLowerInvariant() switch
        {
            "libx264" or "h264_nvenc" or "h264_qsv" or "h264_videotoolbox" or "h264_amf" => "h264_mp4toannexb",
            "libx265" or "hevc_nvenc" or "hevc_qsv" or "hevc_videotoolbox" or "hevc_amf" => "hevc_mp4toannexb",
            _ => ""
        };
    }
}
