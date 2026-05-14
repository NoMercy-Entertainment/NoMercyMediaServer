namespace NoMercy.Encoder.Profiles;

using Codecs;

public static class ContainerCompatibility
{
    private static readonly Dictionary<Container, HashSet<VideoCodecType>> VideoMatrix = new()
    {
        [Container.Mkv] =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ],
        [Container.Mp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
        // HEVC and AV1 over MPEG-TS are valid per the HLS spec (hvc1/hev1 stream
        // type identifiers). FFmpeg muxes them and VLC + every modern HLS client
        // demuxes them. Earlier H264-only matrix forced HEVC presets into fMP4
        // (.m4s segments) which VLC handles poorly.
        [Container.HlsTs] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
        [Container.HlsFmp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
        [Container.Dash] =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ],
    };

    private static readonly Dictionary<Container, HashSet<AudioCodecType>> AudioMatrix = new()
    {
        [Container.Mkv] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Mp3,
            AudioCodecType.Opus,
            AudioCodecType.Flac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.TrueHd,
            AudioCodecType.Dts,
            AudioCodecType.Vorbis,
        ],
        [Container.Mp4] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Mp3,
        ],
        [Container.HlsTs] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Mp3,
        ],
        [Container.HlsFmp4] = [AudioCodecType.Aac, AudioCodecType.Ac3, AudioCodecType.Eac3],
        [Container.Mp3] = [AudioCodecType.Mp3],
        [Container.Aac] = [AudioCodecType.Aac],
        [Container.Flac] = [AudioCodecType.Flac],
        [Container.Ogg] = [AudioCodecType.Vorbis, AudioCodecType.Opus, AudioCodecType.Flac],
        [Container.Mka] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Mp3,
            AudioCodecType.Opus,
            AudioCodecType.Flac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.TrueHd,
            AudioCodecType.Dts,
            AudioCodecType.Vorbis,
        ],
        [Container.AudioHlsTs] = [AudioCodecType.Aac, AudioCodecType.Mp3],
        [Container.AudioHlsFmp4] = [AudioCodecType.Aac, AudioCodecType.Eac3],
        [Container.Dash] = [AudioCodecType.Aac, AudioCodecType.Eac3, AudioCodecType.Opus],
    };

    private static readonly HashSet<VideoCodecType> CmafVideo =
    [
        VideoCodecType.H264,
        VideoCodecType.H265,
        VideoCodecType.Av1,
    ];
    private static readonly HashSet<AudioCodecType> CmafAudio =
    [
        AudioCodecType.Aac,
        AudioCodecType.Eac3,
    ];

    public static bool SupportsVideo(Container container, VideoCodecType codec) =>
        VideoMatrix.TryGetValue(container, out HashSet<VideoCodecType>? set) && set.Contains(codec);

    public static bool SupportsAudio(Container container, AudioCodecType codec) =>
        AudioMatrix.TryGetValue(container, out HashSet<AudioCodecType>? set) && set.Contains(codec);

    public static bool IsCmafCompatible(VideoCodecType codec) => CmafVideo.Contains(codec);

    public static bool IsCmafCompatible(AudioCodecType codec) => CmafAudio.Contains(codec);
}
