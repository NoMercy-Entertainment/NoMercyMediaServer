namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Single source of truth for "given an FFmpeg encoder name, what codec family
/// does it belong to?" Same logic was previously reimplemented in three places
/// — HlsCodecsStringBuilder, V2ProfileFactory.ParseVideoCodec, and
/// PlanStage.VideoCodecFamilyToken — each with slightly different fallbacks
/// and odd cases. Funnelling them through this classifier means future encoder
/// names (e.g. AMD AV1 variants, AppleSilicon RT codecs) only need to be
/// taught once.
/// </summary>
public static class CodecFamilyClassifier
{
    /// <summary>
    /// Classifies a video encoder name into its <see cref="VideoCodecType"/>.
    /// Returns null if the encoder string doesn't match any known family —
    /// callers decide whether to fall back or throw.
    /// </summary>
    public static VideoCodecType? ClassifyVideo(string encoderName)
    {
        if (string.IsNullOrWhiteSpace(encoderName))
            return null;

        string lower = encoderName.ToLowerInvariant();

        if (lower == "copy" || lower.Contains("passthrough"))
            return VideoCodecType.Copy;
        if (lower.Contains("264") || lower.Contains("avc") || lower.Contains("h264"))
            return VideoCodecType.H264;
        if (lower.Contains("265") || lower.Contains("hevc"))
            return VideoCodecType.H265;
        if (lower.Contains("av1") || lower.Contains("aom") || lower.Contains("svtav1"))
            return VideoCodecType.Av1;
        if (lower.Contains("vp9") || lower.Contains("libvpx"))
            return VideoCodecType.Vp9;

        return null;
    }

    /// <summary>
    /// Classifies an audio encoder name into its <see cref="AudioCodecType"/>.
    /// Returns null if the encoder string doesn't match any known family.
    /// </summary>
    public static AudioCodecType? ClassifyAudio(string encoderName)
    {
        if (string.IsNullOrWhiteSpace(encoderName))
            return null;

        string lower = encoderName.ToLowerInvariant();

        if (lower == "copy" || lower.Contains("passthrough"))
            return AudioCodecType.Copy;
        if (lower.Contains("aac") || lower.Contains("fdk"))
            return AudioCodecType.Aac;
        if (lower.Contains("opus"))
            return AudioCodecType.Opus;
        if (lower.Contains("flac"))
            return AudioCodecType.Flac;
        if (lower.Contains("eac3") || lower.Contains("e-ac3"))
            return AudioCodecType.Eac3;
        if (lower.Contains("ac3") || lower.Contains("dolby"))
            return AudioCodecType.Ac3;
        if (lower.Contains("mp3") || lower.Contains("lame"))
            return AudioCodecType.Mp3;
        if (lower.Contains("vorbis"))
            return AudioCodecType.Vorbis;
        if (lower.Contains("truehd"))
            return AudioCodecType.TrueHd;
        if (lower.Contains("dts") || lower.Contains("dca"))
            return AudioCodecType.Dts;

        return null;
    }

    /// <summary>
    /// Short codec family token used in segment / folder names: <c>avc</c>,
    /// <c>hevc</c>, <c>av1</c>, <c>vp9</c>. Falls back to the lowercased
    /// encoder name when no family matches.
    /// </summary>
    public static string FamilyToken(string encoderName) =>
        ClassifyVideo(encoderName) switch
        {
            VideoCodecType.H264 => "avc",
            VideoCodecType.H265 => "hevc",
            VideoCodecType.Av1 => "av1",
            VideoCodecType.Vp9 => "vp9",
            _ => encoderName.ToLowerInvariant(),
        };
}
