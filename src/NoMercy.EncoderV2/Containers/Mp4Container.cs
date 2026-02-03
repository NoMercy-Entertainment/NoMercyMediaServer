using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Containers;

/// <summary>
/// MP4 container format
/// </summary>
public sealed class Mp4Container : ContainerBase
{
    public override string FormatName => "mp4";
    public override string DisplayName => "MP4";
    public override string Extension => ".mp4";
    public override string MimeType => "video/mp4";
    public override bool SupportsStreaming => true;

    public override IReadOnlyList<string> CompatibleVideoCodecs =>
    [
        "libx264", "h264_nvenc", "h264_qsv", "h264_videotoolbox", "h264_amf",
        "libx265", "hevc_nvenc", "hevc_qsv", "hevc_videotoolbox", "hevc_amf",
        "libaom-av1", "libsvtav1", "av1_nvenc", "av1_qsv"
    ];

    public override IReadOnlyList<string> CompatibleAudioCodecs =>
    [
        "aac", "libfdk_aac", "ac3", "eac3", "mp3", "libopus", "flac"
    ];

    public override IReadOnlyList<string> CompatibleSubtitleCodecs =>
    [
        "mov_text"
    ];

    /// <summary>
    /// Move MOOV atom to beginning for streaming (faststart)
    /// </summary>
    public bool FastStart { get; set; } = true;

    /// <summary>
    /// Brand for MP4 container
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// Write edit list (edts)
    /// </summary>
    public bool WriteEditList { get; set; } = true;

    /// <summary>
    /// Fragment the output (for DASH compatibility)
    /// </summary>
    public bool Fragment { get; set; }

    /// <summary>
    /// Fragment duration in microseconds
    /// </summary>
    public long? FragmentDuration { get; set; }

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> args = ["-f", FormatName];

        if (FastStart)
        {
            args.AddRange(["-movflags", "+faststart"]);
        }

        if (!WriteEditList)
        {
            args.AddRange(["-movflags", "+negative_cts_offsets"]);
        }

        if (Fragment)
        {
            List<string> movflags = ["frag_keyframe", "empty_moov"];
            args.AddRange(["-movflags", string.Join("+", movflags)]);

            if (FragmentDuration.HasValue)
            {
                args.AddRange(["-frag_duration", FragmentDuration.Value.ToString()]);
            }
        }

        if (!string.IsNullOrEmpty(Brand))
        {
            args.AddRange(["-brand", Brand]);
        }

        return args;
    }
}

/// <summary>
/// Fragmented MP4 container (for DASH and fMP4 HLS)
/// </summary>
public sealed class FragmentedMp4Container : ContainerBase
{
    public override string FormatName => "mp4";
    public override string DisplayName => "Fragmented MP4";
    public override string Extension => ".mp4";
    public override string MimeType => "video/mp4";
    public override bool SupportsStreaming => true;

    public override IReadOnlyList<string> CompatibleVideoCodecs =>
    [
        "libx264", "h264_nvenc", "h264_qsv", "h264_videotoolbox", "h264_amf",
        "libx265", "hevc_nvenc", "hevc_qsv", "hevc_videotoolbox", "hevc_amf",
        "libaom-av1", "libsvtav1", "av1_nvenc", "av1_qsv"
    ];

    public override IReadOnlyList<string> CompatibleAudioCodecs =>
    [
        "aac", "libfdk_aac", "ac3", "eac3", "libopus"
    ];

    public override IReadOnlyList<string> CompatibleSubtitleCodecs =>
    [
        "mov_text", "webvtt"
    ];

    /// <summary>
    /// Fragment duration in milliseconds
    /// </summary>
    public int FragmentDuration { get; set; } = 4000;

    /// <summary>
    /// Whether to use default base moof
    /// </summary>
    public bool DefaultBaseMoof { get; set; } = true;

    public override IReadOnlyList<string> BuildArguments()
    {
        List<string> movflags = ["frag_keyframe", "empty_moov"];

        if (DefaultBaseMoof)
        {
            movflags.Add("default_base_moof");
        }

        return
        [
            "-f", FormatName,
            "-movflags", string.Join("+", movflags),
            "-frag_duration", (FragmentDuration * 1000).ToString() // Convert to microseconds
        ];
    }
}
