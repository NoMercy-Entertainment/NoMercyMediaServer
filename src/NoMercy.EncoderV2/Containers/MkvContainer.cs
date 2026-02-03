using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Containers;

/// <summary>
/// Matroska (MKV) container format
/// </summary>
public sealed class MkvContainer : ContainerBase
{
    public override string FormatName => "matroska";
    public override string DisplayName => "Matroska (MKV)";
    public override string Extension => ".mkv";
    public override string MimeType => "video/x-matroska";
    public override bool SupportsStreaming => true;

    public override IReadOnlyList<string> CompatibleVideoCodecs =>
    [
        // H.264
        "libx264", "h264_nvenc", "h264_qsv", "h264_videotoolbox", "h264_amf",
        // H.265
        "libx265", "hevc_nvenc", "hevc_qsv", "hevc_videotoolbox", "hevc_amf",
        // AV1
        "libaom-av1", "libsvtav1", "av1_nvenc", "av1_qsv",
        // VP9
        "libvpx-vp9",
        // Others
        "copy"
    ];

    public override IReadOnlyList<string> CompatibleAudioCodecs =>
    [
        "aac", "libfdk_aac", "ac3", "eac3", "libopus", "libvorbis",
        "flac", "libmp3lame", "truehd", "dts", "copy"
    ];

    public override IReadOnlyList<string> CompatibleSubtitleCodecs =>
    [
        "srt", "ass", "webvtt", "copy"
    ];

    /// <summary>
    /// Reserve space at the beginning for better seeking
    /// </summary>
    public int? ReserveIndexSpace { get; set; }

    /// <summary>
    /// Cluster size limit in bytes
    /// </summary>
    public long? ClusterSizeLimit { get; set; }

    /// <summary>
    /// Cluster time limit in milliseconds
    /// </summary>
    public long? ClusterTimeLimit { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-f", FormatName];

        if (ReserveIndexSpace.HasValue)
        {
            args.AddRange(["-reserve_index_space", ReserveIndexSpace.Value.ToString()]);
        }

        if (ClusterSizeLimit.HasValue)
        {
            args.AddRange(["-cluster_size_limit", ClusterSizeLimit.Value.ToString()]);
        }

        if (ClusterTimeLimit.HasValue)
        {
            args.AddRange(["-cluster_time_limit", ClusterTimeLimit.Value.ToString()]);
        }

        return args;
    }
}

/// <summary>
/// WebM container format (subset of Matroska)
/// </summary>
public sealed class WebMContainer : ContainerBase
{
    public override string FormatName => "webm";
    public override string DisplayName => "WebM";
    public override string Extension => ".webm";
    public override string MimeType => "video/webm";
    public override bool SupportsStreaming => true;

    public override IReadOnlyList<string> CompatibleVideoCodecs =>
    [
        "libvpx-vp9", "libaom-av1", "libsvtav1"
    ];

    public override IReadOnlyList<string> CompatibleAudioCodecs =>
    [
        "libopus", "libvorbis"
    ];

    public override IReadOnlyList<string> CompatibleSubtitleCodecs =>
    [
        "webvtt"
    ];

    /// <summary>
    /// Enable DASH-compatible output
    /// </summary>
    public bool DashMode { get; set; }

    /// <summary>
    /// Cluster size limit in bytes
    /// </summary>
    public long? ClusterSizeLimit { get; set; }

    /// <summary>
    /// Cluster time limit in milliseconds
    /// </summary>
    public long? ClusterTimeLimit { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-f", FormatName];

        if (DashMode)
        {
            args.AddRange(["-dash", "1"]);
        }

        if (ClusterSizeLimit.HasValue)
        {
            args.AddRange(["-cluster_size_limit", ClusterSizeLimit.Value.ToString()]);
        }

        if (ClusterTimeLimit.HasValue)
        {
            args.AddRange(["-cluster_time_limit", ClusterTimeLimit.Value.ToString()]);
        }

        return args;
    }

    public override ValidationResult ValidateCodecs(IVideoCodec? videoCodec, IAudioCodec? audioCodec, ISubtitleCodec? subtitleCodec)
    {
        List<string> errors = [];

        if (videoCodec != null && !CompatibleVideoCodecs.Contains(videoCodec.Name))
        {
            errors.Add($"WebM only supports VP9 and AV1 video codecs, not '{videoCodec.Name}'");
        }

        if (audioCodec != null && !CompatibleAudioCodecs.Contains(audioCodec.Name))
        {
            errors.Add($"WebM only supports Opus and Vorbis audio codecs, not '{audioCodec.Name}'");
        }

        if (subtitleCodec != null && !CompatibleSubtitleCodecs.Contains(subtitleCodec.Name))
        {
            errors.Add($"WebM only supports WebVTT subtitles, not '{subtitleCodec.Name}'");
        }

        return errors.Count > 0 ? ValidationResult.Failure([.. errors]) : ValidationResult.Success();
    }
}
