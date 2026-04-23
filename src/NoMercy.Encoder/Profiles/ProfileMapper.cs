namespace NoMercy.Encoder.Profiles;

using NoMercy.Encoder.Audio;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;

/// <summary>
/// Maps V1 encoder profile types (IVideoProfile, IAudioProfile, ISubtitleProfile)
/// to V3 pipeline input types (VideoOutput, AudioOutput, SubtitleOutput).
/// The V1 format is the user-facing, editable format stored in the database.
/// The V3 types are internal pipeline inputs.
/// </summary>
public static class ProfileMapper
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
        OutputFormat format = container.ToLowerInvariant() switch
        {
            "m3u8" or "hls" => OutputFormat.Hls,
            "mkv" or "matroska" => OutputFormat.Mkv,
            // m4a / aac are audio-only MP4 — the container is still MP4.
            // Mp4OutputStrategy detects audio-only and writes .m4a accordingly.
            "mp4" or "m4a" or "aac" => OutputFormat.Mp4,
            "dash" or "mpd" => OutputFormat.Dash,
            "mp3" => OutputFormat.Mp3,
            "flac" => OutputFormat.Flac,
            "ogg" or "oga" or "opus" => OutputFormat.Ogg,
            _ => OutputFormat.Hls,
        };

        VideoOutput[] video = videoProfiles.Select(MapVideo).ToArray();
        AudioOutput[] audio = audioProfiles.Select(MapAudio).ToArray();
        SubtitleOutput[] subtitles = subtitleProfiles.Select(MapSubtitle).ToArray();

        ThumbnailOutput? thumbnails = thumbnailProfile is not null
            ? new ThumbnailOutput(thumbnailProfile.Width, thumbnailProfile.IntervalSeconds)
            : null;

        EncodeMode mode = ParseEncodeMode(encodeMode);

        return new(
            Id: id,
            Name: name,
            Format: format,
            VideoOutputs: video,
            AudioOutputs: audio,
            SubtitleOutputs: subtitles,
            Thumbnails: thumbnails,
            EncodeMode: mode,
            AutoLadder: autoLadder
        );
    }

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
        bool tenBit =
            v.ColorSpace.Contains("10", StringComparison.Ordinal)
            || v.ColorSpace.Contains("p010", StringComparison.OrdinalIgnoreCase);

        Dictionary<string, string>? customArgs =
            v.CustomArguments.Length > 0
                ? v.CustomArguments.ToDictionary(c => c.key, c => c.Val)
                : null;

        return new(
            Codec: codec,
            Width: v.Width,
            Height: v.Height > 0 ? v.Height : null,
            BitrateKbps: v.Bitrate,
            Crf: v.Crf,
            Preset: string.IsNullOrEmpty(v.Preset) ? null : v.Preset,
            Profile: string.IsNullOrEmpty(v.Profile) ? null : v.Profile,
            Level: string.IsNullOrEmpty(v.Level) ? null : v.Level,
            ConvertHdrToSdr: v.ConvertHdrToSdr,
            KeyframeIntervalSeconds: v.KeyInt > 0 ? v.KeyInt : 2,
            TenBit: tenBit,
            SegmentNameTemplate: string.IsNullOrEmpty(v.SegmentName)
                ? ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
                : v.SegmentName,
            PlaylistNameTemplate: string.IsNullOrEmpty(v.PlaylistName)
                ? ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
                : v.PlaylistName,
            Tune: string.IsNullOrEmpty(v.Tune) ? null : v.Tune,
            ColorSpace: string.IsNullOrEmpty(v.ColorSpace) ? null : v.ColorSpace,
            CustomArguments: customArgs
        );
    }

    private static AudioOutput MapAudio(V1AudioProfile a)
    {
        AudioCodecType codec = ParseAudioCodec(a.Codec);

        Dictionary<string, string>? customArgs =
            a.CustomArguments.Length > 0
                ? a.CustomArguments.ToDictionary(c => c.key, c => c.Val)
                : null;

        // V1 profiles don't store a bitrate, so we use the codec's own default.
        // Previously hardcoded to 192 kbps, which was fine for AAC stereo but
        // too low for AC3 5.1 (spec default 384), laughably low for EAC3 7.1
        // (default 640), and meaningless on lossless codecs (flac, truehd use 0).
        // Using the codec's DefaultBitrateKbps keeps every output on its own
        // reasonable default instead of forcing every codec into one number.
        AudioEncoderInfo encoder = AudioCodecDefinitions.GetEncoder(codec);
        int bitrate = encoder.IsLossless ? 0 : encoder.DefaultBitrateKbps;

        return new(
            Codec: codec,
            BitrateKbps: bitrate,
            Channels: a.Channels > 0 ? a.Channels : 2,
            SampleRateHz: a.SampleRate > 0 ? a.SampleRate : 48000,
            AllowedLanguages: a.AllowedLanguages,
            Loudness: ParseLoudness(a.Loudness),
            Downmix: ParseDownmix(a.Downmix),
            CustomPanMatrix: a.CustomPanMatrix,
            SegmentNameTemplate: string.IsNullOrEmpty(a.SegmentName)
                ? ":type:_:language:_:codec:/:type:_:language:_:codec:"
                : a.SegmentName,
            PlaylistNameTemplate: string.IsNullOrEmpty(a.PlaylistName)
                ? ":type:_:language:_:codec:/:type:_:language:_:codec:"
                : a.PlaylistName,
            CustomArguments: customArgs
        );
    }

    private static DownmixMode ParseDownmix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DownmixMode.Auto;

        return value.ToLowerInvariant() switch
        {
            "stereo" or "stereo_itur128" or "itur128" or "bs775" => DownmixMode.StereoItuR128,
            "mono" => DownmixMode.Mono,
            "custom" => DownmixMode.Custom,
            "auto" or "none" or "off" => DownmixMode.Auto,
            _ => DownmixMode.Auto,
        };
    }

    private static LoudnessMode ParseLoudness(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LoudnessMode.None;

        return value.ToLowerInvariant() switch
        {
            "ebu" or "ebur128" or "ebu_r128" or "ebu-r128" or "r128" => LoudnessMode.EbuR128,
            "replaygain" or "replay_gain" or "replay-gain" or "rg" => LoudnessMode.ReplayGain,
            "custom" => LoudnessMode.Custom,
            "none" or "off" => LoudnessMode.None,
            _ => LoudnessMode.None,
        };
    }

    private static SubtitleOutput MapSubtitle(V1SubtitleProfile s)
    {
        SubtitleCodecType codec = s.Codec.ToLowerInvariant() switch
        {
            "webvtt" or "vtt" => SubtitleCodecType.WebVtt,
            "ass" or "ssa" => SubtitleCodecType.Ass,
            "srt" or "subrip" => SubtitleCodecType.Srt,
            _ => SubtitleCodecType.WebVtt,
        };

        Dictionary<string, string>? customArgs =
            s.CustomArguments.Length > 0
                ? s.CustomArguments.ToDictionary(c => c.key, c => c.Val)
                : null;

        return new(
            Codec: codec,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: s.AllowedLanguages,
            PlaylistNameTemplate: string.IsNullOrEmpty(s.PlaylistName)
                ? "subtitles/:filename:.:language:.:variant:"
                : s.PlaylistName,
            CustomArguments: customArgs
        );
    }

    private static VideoCodecType ParseVideoCodec(string codec)
    {
        string lower = codec.ToLowerInvariant();
        if (lower.Contains("264") || lower.Contains("avc"))
            return VideoCodecType.H264;
        if (lower.Contains("265") || lower.Contains("hevc"))
            return VideoCodecType.H265;
        if (lower.Contains("av1") || lower.Contains("aom"))
            return VideoCodecType.Av1;
        if (lower.Contains("vp9"))
            return VideoCodecType.Vp9;
        return VideoCodecType.H264;
    }

    private static AudioCodecType ParseAudioCodec(string codec)
    {
        string lower = codec.ToLowerInvariant();
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
        return AudioCodecType.Aac;
    }
}
