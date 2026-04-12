namespace NoMercy.Encoder.V3.Pipeline;

using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Profiles;

public class StreamActionResolver
{
    // Maps AudioCodecType enum values to the ffprobe codec_name strings that
    // ffprobe returns in stream.codec_name.  A type may map to multiple names
    // (e.g. DTS has two aliases).
    private static readonly Dictionary<AudioCodecType, HashSet<string>> AudioCodecNames = new()
    {
        [AudioCodecType.Aac] = ["aac"],
        [AudioCodecType.Flac] = ["flac"],
        [AudioCodecType.Opus] = ["opus"],
        [AudioCodecType.Ac3] = ["ac3"],
        [AudioCodecType.Eac3] = ["eac3"],
        [AudioCodecType.Mp3] = ["mp3"],
        [AudioCodecType.Vorbis] = ["vorbis"],
        [AudioCodecType.TrueHd] = ["truehd"],
        [AudioCodecType.Dts] = ["dts", "dca"],
    };

    // Lossless source codecs: if the source stream carries one of these names
    // and the profile requests any lossy codec, we must always transcode.
    private static readonly HashSet<string> LosslessSourceCodecs = ["flac", "truehd"];

    // Maps VideoCodecType enum values to the ffprobe codec_name strings.
    private static readonly Dictionary<VideoCodecType, HashSet<string>> VideoCodecNames = new()
    {
        [VideoCodecType.H264] = ["h264", "avc"],
        [VideoCodecType.H265] = ["hevc", "h265"],
        [VideoCodecType.Vp9] = ["vp9"],
        [VideoCodecType.Av1] = ["av1"],
    };

    // Lossy target codecs — every AudioCodecType NOT in the lossless set.
    // TrueHD and FLAC are lossless; everything else we treat as lossy for the
    // purpose of the "lossless source + lossy target → always transcode" rule.
    private static readonly HashSet<AudioCodecType> LosslessTargetCodecs =
    [
        AudioCodecType.Flac,
        AudioCodecType.TrueHd,
    ];

    // -------------------------------------------------------------------------
    // Audio passthrough (spec section 35.1)
    // -------------------------------------------------------------------------
    // Copy when ALL of:
    //   1. source codec_name matches one of the profile codec's ffprobe names
    //   2. source bitrate >= profile bitrate
    //   3. source channels >= profile channels
    //
    // Lossless source + lossy target → always Transcode (overrides codec match).
    // Any mismatch → Transcode.
    public StreamAction ResolveAudio(
        AudioStreamInfo source,
        AudioOutput profile,
        OutputFormat format
    )
    {
        string sourceCodec = source.Codec.ToLowerInvariant();

        // Lossless source heading toward a lossy profile → must transcode.
        bool sourceLossless = LosslessSourceCodecs.Contains(sourceCodec);
        bool targetLossy = !LosslessTargetCodecs.Contains(profile.Codec);

        if (sourceLossless && targetLossy)
        {
            return StreamAction.Transcode;
        }

        // Codec name must match.
        bool codecMatches =
            AudioCodecNames.TryGetValue(profile.Codec, out HashSet<string>? names)
            && names.Contains(sourceCodec);

        if (!codecMatches)
        {
            return StreamAction.Transcode;
        }

        // Bitrate and channel count must be sufficient.
        if (source.BitRateKbps < profile.BitrateKbps)
        {
            return StreamAction.Transcode;
        }

        if (source.Channels < profile.Channels)
        {
            return StreamAction.Transcode;
        }

        return StreamAction.Copy;
    }

    // -------------------------------------------------------------------------
    // Subtitle decision matrix (spec section 38)
    // -------------------------------------------------------------------------
    // BurnIn mode → always Transcode (it becomes a video filter, not a stream).
    //
    // Text-based subtitles (SRT, ASS, WebVTT, mov_text, …):
    //   MKV  → Copy
    //   HLS  → Extract (convert to WebVTT sidecar)
    //   MP4  → Extract (sidecar)
    //   DASH → Extract (WebVTT sidecar)
    //
    // Bitmap subtitles (PGS/HDMV, VOBSUB, DVB-Bitmap):
    //   MKV         → Copy
    //   Everything else → Transcode (burn-in is the only viable option)
    public StreamAction ResolveSubtitle(
        SubtitleStreamInfo source,
        SubtitleOutput profile,
        OutputFormat format
    )
    {
        // BurnIn is a video filter — callers treat this as Transcode.
        if (profile.Mode == SubtitleMode.BurnIn)
        {
            return StreamAction.Transcode;
        }

        if (source.IsTextBased)
        {
            return format switch
            {
                OutputFormat.Mkv => StreamAction.Copy,
                OutputFormat.Hls => StreamAction.Extract,
                OutputFormat.Mp4 => StreamAction.Extract,
                OutputFormat.Dash => StreamAction.Extract,
                _ => StreamAction.Extract,
            };
        }

        // Bitmap subtitle.
        return format switch
        {
            OutputFormat.Mkv => StreamAction.Copy,
            // Bitmap subs cannot be embedded in HLS/MP4/DASH; burn in.
            _ => StreamAction.Transcode,
        };
    }

    // -------------------------------------------------------------------------
    // Video passthrough (remux only — rare)
    // -------------------------------------------------------------------------
    // Copy when ALL of:
    //   1. source codec_name matches one of the profile codec's ffprobe names
    //   2. source width == profile width AND source height <= profile height
    //      (profile height may be null meaning "keep source height")
    //   3. source bitrate >= profile bitrate
    public StreamAction ResolveVideo(VideoStreamInfo source, VideoOutput profile)
    {
        string sourceCodec = source.Codec.ToLowerInvariant();

        bool codecMatches =
            VideoCodecNames.TryGetValue(profile.Codec, out HashSet<string>? names)
            && names.Contains(sourceCodec);

        if (!codecMatches)
        {
            return StreamAction.Transcode;
        }

        // Resolution must match.  Profile.Height is nullable; when null it
        // means "same as source", so we skip the height check.
        bool widthMatches = source.Width == profile.Width;
        bool heightMatches = profile.Height is null || source.Height == profile.Height;

        if (!widthMatches || !heightMatches)
        {
            return StreamAction.Transcode;
        }

        // Source bitrate must be at least what the profile targets.
        if (source.BitRateKbps < profile.BitrateKbps)
        {
            return StreamAction.Transcode;
        }

        return StreamAction.Copy;
    }
}
