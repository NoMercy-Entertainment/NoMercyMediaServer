using NoMercy.Database;
using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Reverse of <see cref="V2ProfileFactory"/>: maps a V2 <see cref="EncodingProfile"/>
/// down to the V1 column shape so a matching <c>EncoderProfile</c> row can be
/// materialised. Existed once, was deleted with the V2.5 cleanup, restored
/// because the encode pipeline + folder picker still read from V1 via the
/// <c>EncoderProfileFolder</c> foreign key.
/// </summary>
public static class V1ProfileFactory
{
    public readonly record struct V1Shape(
        string Container,
        IVideoProfile[] VideoProfiles,
        IAudioProfile[] AudioProfiles,
        ISubtitleProfile[] SubtitleProfiles
    );

    public static V1Shape FromV2(EncodingProfile profile)
    {
        string container = SerializeContainer(profile.Container);

        List<IVideoProfile> videos = [];
        if (profile.Video is not null)
            videos.Add(MapVideo(profile.Video));

        if (profile.Ladder is not null && profile.Video is not null)
        {
            if (
                profile.Ladder.Mode == LadderMode.Manual
                && profile.Ladder.Rungs is { Length: > 0 } rungs
            )
            {
                foreach (LadderRung rung in rungs)
                    videos.Add(MapRung(rung, profile.Video));
            }
            else if (profile.Ladder.Mode == LadderMode.Auto && profile.Ladder.AutoConfig is { } cfg)
            {
                foreach (LadderTier tier in cfg.Tiers)
                    videos.Add(MapTier(tier, profile.Video, cfg));
            }
        }

        IAudioProfile[] audios = profile.Audio.Select(MapAudio).ToArray();
        ISubtitleProfile[] subtitles = profile.Subtitles.Select(MapSubtitle).ToArray();

        return new V1Shape(container, [.. videos], audios, subtitles);
    }

    private static string SerializeContainer(Container c) =>
        c switch
        {
            Container.Mkv => "mkv",
            Container.Mp4 => "mp4",
            Container.HlsTs or Container.HlsFmp4 => "m3u8",
            Container.AudioHlsTs or Container.AudioHlsFmp4 => "m3u8",
            Container.Dash => "dash",
            Container.Mp3 => "mp3",
            Container.Aac => "aac",
            Container.Flac => "flac",
            Container.Ogg => "ogg",
            Container.Mka => "mka",
            Container.Mks => "mks",
            _ => "m3u8",
        };

    private static string SerializeVideoCodec(VideoCodecType codec) =>
        codec switch
        {
            VideoCodecType.H264 => "h264",
            VideoCodecType.H265 => "hevc",
            VideoCodecType.Av1 => "av1",
            VideoCodecType.Vp9 => "vp9",
            VideoCodecType.Copy => "copy",
            _ => "h264",
        };

    private static string SerializeAudioCodec(AudioCodecType codec) =>
        codec switch
        {
            AudioCodecType.Aac => "aac",
            AudioCodecType.Flac => "flac",
            AudioCodecType.Opus => "opus",
            AudioCodecType.Ac3 => "ac3",
            AudioCodecType.Eac3 => "eac3",
            AudioCodecType.Mp3 => "mp3",
            AudioCodecType.Vorbis => "vorbis",
            AudioCodecType.TrueHd => "truehd",
            AudioCodecType.Dts => "dts",
            AudioCodecType.Copy => "copy",
            _ => "aac",
        };

    private static string SerializeSubtitleCodec(SubtitleCodecType codec) =>
        codec switch
        {
            SubtitleCodecType.WebVtt => "webvtt",
            SubtitleCodecType.Ass => "ass",
            SubtitleCodecType.Srt => "srt",
            SubtitleCodecType.Copy => "copy",
            _ => "webvtt",
        };

    private static string SerializeCodecProfile(CodecProfile profile) =>
        profile switch
        {
            CodecProfile.Baseline => "baseline",
            CodecProfile.Main => "main",
            CodecProfile.Main10 => "main10",
            CodecProfile.High => "high",
            CodecProfile.High10 => "high10",
            _ => string.Empty,
        };

    private static IVideoProfile MapVideo(VideoOutput v) =>
        new()
        {
            Codec = SerializeVideoCodec(v.Codec),
            Bitrate = v.BitrateKbps,
            Width = v.Width,
            Height = v.Height ?? 0,
            Framerate = 0,
            Preset = v.Preset ?? string.Empty,
            Profile = SerializeCodecProfile(v.CodecProfile),
            Tune = v.Tune ?? string.Empty,
            Level = v.Level ?? string.Empty,
            SegmentName = v.SegmentNameTemplate,
            PlaylistName = v.PlaylistNameTemplate,
            ColorSpace = v.PixelFormat ?? string.Empty,
            Crf = v.Crf,
            KeyInt = v.KeyframeIntervalSeconds,
            Opts = [],
            CustomArguments = v.CustomArguments is null
                ? []
                : v.CustomArguments.Select(kv => (kv.Key, kv.Value)).ToArray(),
            ConvertHdrToSdr = v.ConvertHdrToSdr,
        };

    private static IVideoProfile MapRung(LadderRung rung, VideoOutput reference) =>
        new()
        {
            Codec = SerializeVideoCodec(rung.Codec),
            Bitrate = rung.BitrateKbps,
            Width = rung.Width,
            Height = rung.Height,
            Framerate = (int)rung.Framerate,
            Preset = rung.Preset ?? reference.Preset ?? string.Empty,
            Profile = SerializeCodecProfile(rung.CodecProfile),
            Tune = reference.Tune ?? string.Empty,
            Level = reference.Level ?? string.Empty,
            SegmentName = reference.SegmentNameTemplate,
            PlaylistName = reference.PlaylistNameTemplate,
            ColorSpace = rung.PixelFormat ?? reference.PixelFormat ?? string.Empty,
            Crf = reference.Crf,
            KeyInt = reference.KeyframeIntervalSeconds,
            Opts = [],
            CustomArguments = [],
            ConvertHdrToSdr = reference.ConvertHdrToSdr,
        };

    private static IVideoProfile MapTier(
        LadderTier tier,
        VideoOutput reference,
        AutoLadderConfig cfg
    )
    {
        int bitrate = (
            reference.Codec,
            tier.RecommendedBitrateH264Kbps,
            tier.RecommendedBitrateHevcKbps,
            tier.RecommendedBitrateAv1Kbps
        ) switch
        {
            (VideoCodecType.H264, int b, _, _) => b,
            (VideoCodecType.H265, _, int b, _) => b,
            (VideoCodecType.Av1, _, _, int b) => b,
            _ => tier.RecommendedBitrateH264Kbps ?? 0,
        };

        return new()
        {
            Codec = SerializeVideoCodec(reference.Codec),
            Bitrate = bitrate,
            Width = tier.Width,
            Height = tier.Height,
            Framerate = 0,
            Preset = reference.Preset ?? string.Empty,
            Profile = SerializeCodecProfile(reference.CodecProfile),
            Tune = reference.Tune ?? string.Empty,
            Level = reference.Level ?? string.Empty,
            SegmentName = reference.SegmentNameTemplate,
            PlaylistName = reference.PlaylistNameTemplate,
            ColorSpace = reference.PixelFormat ?? string.Empty,
            Crf = cfg.Crf,
            KeyInt = reference.KeyframeIntervalSeconds,
            Opts = [],
            CustomArguments = [],
            ConvertHdrToSdr = reference.ConvertHdrToSdr,
        };
    }

    private static IAudioProfile MapAudio(AudioOutput a) =>
        new()
        {
            Codec = SerializeAudioCodec(a.Codec),
            Channels = a.Channels,
            SampleRate = a.SampleRateHz,
            SegmentName = a.SegmentNameTemplate,
            PlaylistName = a.PlaylistNameTemplate,
            AllowedLanguages = a.AllowedLanguages,
            Opts = [],
            CustomArguments = a.CustomArguments is null
                ? []
                : a.CustomArguments.Select(kv => (kv.Key, kv.Value)).ToArray(),
            Loudness = a.Loudness?.Mode.ToString().ToLowerInvariant(),
            Downmix = a.Downmix?.Mode.ToString().ToLowerInvariant(),
            CustomPanMatrix = a.Downmix?.CustomPanMatrix,
        };

    private static ISubtitleProfile MapSubtitle(SubtitleOutput s) =>
        new()
        {
            Codec = SerializeSubtitleCodec(s.Codec),
            SegmentName = string.Empty,
            PlaylistName = s.PlaylistNameTemplate,
            AllowedLanguages = s.AllowedLanguages,
            Opts = [],
            CustomArguments = s.CustomArguments is null
                ? []
                : s.CustomArguments.Select(kv => (kv.Key, kv.Value)).ToArray(),
        };
}
