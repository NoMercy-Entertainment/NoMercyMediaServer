using NoMercy.Encoder.Audio;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Constructs V2 <see cref="EncodingProfile"/> instances from V1 database row
/// values. This is the single authoritative path from V1 DB → V2 pipeline input.
/// </summary>
public static class V2ProfileFactory
{
    public static EncodingProfile FromV1(
        Ulid id,
        string name,
        string container,
        IReadOnlyList<V1VideoProfile> videoProfiles,
        IReadOnlyList<V1AudioProfile> audioProfiles,
        IReadOnlyList<V1SubtitleProfile> subtitleProfiles,
        V1ThumbnailProfile? thumbnailProfile = null,
        string? encodeMode = null,
        bool autoLadder = false
    )
    {
        Container c = ParseContainer(container);
        VideoOutput? video = videoProfiles.Count > 0 ? MapVideo(videoProfiles[0]) : null;

        LadderConfig? ladder = null;
        if (autoLadder)
        {
            ladder = new LadderConfig { Mode = LadderMode.Auto };
        }
        else if (videoProfiles.Count > 1)
        {
            LadderRung[] rungs = videoProfiles
                .Skip(1)
                .Select(v =>
                {
                    VideoOutput mapped = MapVideo(v);
                    return new LadderRung(
                        Width: mapped.Width,
                        Height: mapped.Height ?? 0,
                        Codec: mapped.Codec,
                        BitrateKbps: mapped.BitrateKbps,
                        MaxBitrateKbps: mapped.MaxBitrateKbps ?? 0,
                        BufferSizeKbps: mapped.BufferSizeKbps ?? 0,
                        Framerate: 0,
                        Preset: mapped.Preset,
                        CodecProfile: mapped.CodecProfile,
                        BitDepth: mapped.BitDepth,
                        PixelFormat: mapped.PixelFormat
                    );
                })
                .ToArray();

            ladder = new LadderConfig { Mode = LadderMode.Manual, Rungs = rungs };
        }

        AudioOutput[] audio = audioProfiles.Select(MapAudio).ToArray();
        SubtitleOutput[] subtitles = subtitleProfiles.Select(MapSubtitle).ToArray();

        ThumbnailOutput? thumbnails = thumbnailProfile is not null
            ? new ThumbnailOutput(thumbnailProfile.Width, thumbnailProfile.IntervalSeconds)
            : null;

        EncodeMode mode = ParseEncodeMode(encodeMode);

        return new EncodingProfile(
            Id: id,
            Name: name,
            Container: c,
            Video: video,
            Audio: audio,
            Subtitles: subtitles,
            Thumbnails: thumbnails,
            Ladder: ladder,
            EncodeMode: mode
        );
    }

    // 'm3u8' is just the playlist extension — both HlsTs (mpegts segments) and
    // HlsFmp4 (fMP4 segments) serve M3U8 playlists. Legacy V1 rows store 'm3u8'
    // without distinguishing the segment format, and stale HEVC rows hit the
    // codec/container validator when 'm3u8' parses to HlsTs (mpegts can't carry
    // H265). Default to HlsFmp4 — the modern HLS variant that supports HEVC,
    // HDR, and is recommended by Apple and Google for new content. Callers who
    // explicitly want mpegts segments now use 'hls_ts' / 'ts' / 'mpegts'.
    private static Container ParseContainer(string container) =>
        container.ToLowerInvariant() switch
        {
            "ts" or "mpegts" or "hls_ts" => Container.HlsTs,
            "m3u8" or "hls" or "fmp4" or "hls_fmp4" => Container.HlsFmp4,
            "mkv" or "matroska" => Container.Mkv,
            "mp4" or "m4a" or "aac" => Container.Mp4,
            "dash" or "mpd" => Container.Dash,
            "mp3" => Container.Mp3,
            "flac" => Container.Flac,
            "ogg" or "oga" or "opus" => Container.Ogg,
            _ => Container.HlsFmp4,
        };

    private static EncodeMode ParseEncodeMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EncodeMode.SinglePass;

        return value.ToLowerInvariant() switch
        {
            "2pass" or "twopass" or "two_pass" or "two-pass" => EncodeMode.TwoPass,
            _ => EncodeMode.SinglePass,
        };
    }

    private static VideoOutput MapVideo(V1VideoProfile v)
    {
        VideoCodecType codec = ParseVideoCodec(v.Codec);
        string colorSpace = v.ColorSpace ?? string.Empty;
        bool tenBit =
            colorSpace.Contains("10", StringComparison.Ordinal)
            || colorSpace.Contains("p010", StringComparison.OrdinalIgnoreCase);

        CodecProfile codecProfile = ParseCodecProfile(v.Profile);

        Dictionary<string, string>? customArgs =
            v.CustomArguments.Length > 0
                ? v.CustomArguments.ToDictionary(c => c.key, c => c.Val)
                : null;

        return new VideoOutput(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            Width: v.Width,
            Height: v.Height > 0 ? v.Height : null,
            RateControl: v.Crf > 0 ? RateControlMode.Crf : RateControlMode.Vbr,
            Crf: v.Crf,
            BitrateKbps: v.Bitrate,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: string.IsNullOrEmpty(v.Preset) ? null : v.Preset,
            CodecProfile: codecProfile,
            Level: string.IsNullOrEmpty(v.Level) ? null : v.Level,
            Tune: string.IsNullOrEmpty(v.Tune) ? null : v.Tune,
            BitDepth: tenBit ? 10 : 8,
            PixelFormat: string.IsNullOrEmpty(v.ColorSpace) ? null : v.ColorSpace,
            KeyframeIntervalSeconds: v.KeyInt > 0 ? v.KeyInt : 2,
            ConvertHdrToSdr: v.ConvertHdrToSdr,
            SegmentNameTemplate: string.IsNullOrEmpty(v.SegmentName)
                ? ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
                : v.SegmentName,
            PlaylistNameTemplate: string.IsNullOrEmpty(v.PlaylistName)
                ? ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
                : v.PlaylistName,
            CustomArguments: customArgs
        );
    }

    private static AudioOutput MapAudio(V1AudioProfile a)
    {
        AudioCodecType codec = ParseAudioCodec(a.Codec);

        AudioEncoderInfo encoder = AudioCodecDefinitions.GetEncoder(codec);
        int bitrate = encoder.IsLossless ? 0 : encoder.DefaultBitrateKbps;

        LoudnessConfig? loudness = ParseLoudness(a.Loudness);
        DownmixConfig? downmix = ParseDownmix(a.Downmix, a.CustomPanMatrix);

        Dictionary<string, string>? customArgs =
            a.CustomArguments.Length > 0
                ? a.CustomArguments.ToDictionary(c => c.key, c => c.Val)
                : null;

        return new AudioOutput(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            BitrateKbps: bitrate,
            Channels: a.Channels > 0 ? a.Channels : 2,
            SampleRateHz: a.SampleRate > 0 ? a.SampleRate : 48000,
            AllowedLanguages: a.AllowedLanguages,
            DefaultLanguage: null,
            Loudness: loudness,
            Downmix: downmix,
            SegmentNameTemplate: string.IsNullOrEmpty(a.SegmentName)
                ? ":type:_:language:_:codec:/:type:_:language:_:codec:"
                : a.SegmentName,
            PlaylistNameTemplate: string.IsNullOrEmpty(a.PlaylistName)
                ? ":type:_:language:_:codec:/:type:_:language:_:codec:"
                : a.PlaylistName,
            CustomArguments: customArgs
        );
    }

    private static SubtitleOutput MapSubtitle(V1SubtitleProfile s)
    {
        SubtitleCodecType codec = s.Codec.ToLowerInvariant() switch
        {
            "webvtt" or "vtt" => SubtitleCodecType.WebVtt,
            "ass" or "ssa" => SubtitleCodecType.Ass,
            "srt" or "subrip" => SubtitleCodecType.Srt,
            "copy" or "passthrough" => SubtitleCodecType.Copy,
            _ => SubtitleCodecType.WebVtt,
        };

        Dictionary<string, string>? customArgs =
            s.CustomArguments.Length > 0
                ? s.CustomArguments.ToDictionary(c => c.key, c => c.Val)
                : null;

        return new SubtitleOutput(
            Policy: SubtitlePolicy.Extract,
            Codec: codec,
            AllowedLanguages: s.AllowedLanguages,
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: string.IsNullOrEmpty(s.PlaylistName)
                ? "subtitles/:filename:.:language:.:variant:"
                : s.PlaylistName,
            CustomArguments: customArgs
        );
    }

    private static LoudnessConfig? ParseLoudness(string? value)
    {
        LoudnessMode mode = value?.ToLowerInvariant() switch
        {
            "ebu" or "ebur128" or "ebu_r128" or "ebu-r128" or "r128" => LoudnessMode.EbuR128,
            "replaygain" or "replay_gain" or "replay-gain" or "rg" => LoudnessMode.ReplayGain,
            "custom" => LoudnessMode.Custom,
            _ => LoudnessMode.None,
        };

        return mode == LoudnessMode.None ? null : new LoudnessConfig(mode);
    }

    private static DownmixConfig? ParseDownmix(string? value, string? customPanMatrix)
    {
        DownmixMode mode = value?.ToLowerInvariant() switch
        {
            "stereo" or "stereo_itur128" or "itur128" or "bs775" => DownmixMode.StereoItuR128,
            "mono" => DownmixMode.Mono,
            "custom" => DownmixMode.Custom,
            _ => DownmixMode.Auto,
        };

        return mode == DownmixMode.Auto
            ? null
            : new DownmixConfig(mode, mode == DownmixMode.Custom ? customPanMatrix : null);
    }

    private static CodecProfile ParseCodecProfile(string? profileStr)
    {
        if (string.IsNullOrWhiteSpace(profileStr))
            return CodecProfile.Auto;

        return profileStr.ToLowerInvariant() switch
        {
            "baseline" or "constrained_baseline" or "cb" => CodecProfile.Baseline,
            "main" or "m" => CodecProfile.Main,
            "main10" or "main 10" or "hi10p" => CodecProfile.Main10,
            "high" or "h" => CodecProfile.High,
            "high10" or "high 10" or "high10p" => CodecProfile.High10,
            _ => CodecProfile.Auto,
        };
    }

    private static VideoCodecType ParseVideoCodec(string codec) =>
        CodecFamilyClassifier.ClassifyVideo(codec) ?? VideoCodecType.H264;

    private static AudioCodecType ParseAudioCodec(string codec) =>
        CodecFamilyClassifier.ClassifyAudio(codec) ?? AudioCodecType.Aac;
}
