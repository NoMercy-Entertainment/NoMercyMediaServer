namespace NoMercy.Encoder.Profiles.V2;

using NoMercy.Encoder.Codecs;

public static class CodecDefaults
{
    public record VideoDefaults(int Crf, string Preset, CodecProfile Profile, int BitDepth);

    public record AudioDefaults(int BitrateKbps, int Channels, int SampleRateHz);

    public static VideoDefaults For(VideoCodecType codec) =>
        codec switch
        {
            VideoCodecType.H264 => new(22, "medium", CodecProfile.High, 8),
            VideoCodecType.H265 => new(20, "slow", CodecProfile.Main10, 10),
            VideoCodecType.Av1 => new(30, "6", CodecProfile.Main, 10),
            VideoCodecType.Vp9 => new(32, "good", CodecProfile.Main, 8),
            _ => throw new ArgumentOutOfRangeException(
                nameof(codec),
                codec,
                $"No defaults for {codec}"
            ),
        };

    public static AudioDefaults For(AudioCodecType codec) =>
        codec switch
        {
            AudioCodecType.Aac => new(192, 2, 48000),
            AudioCodecType.Mp3 => new(320, 2, 44100),
            AudioCodecType.Opus => new(128, 2, 48000),
            AudioCodecType.Flac => new(0, 2, 48000),
            AudioCodecType.Ac3 => new(384, 6, 48000),
            AudioCodecType.Eac3 => new(448, 6, 48000),
            AudioCodecType.TrueHd => new(0, 6, 48000),
            AudioCodecType.Dts => new(1536, 6, 48000),
            AudioCodecType.Vorbis => new(192, 2, 48000),
            _ => throw new ArgumentOutOfRangeException(
                nameof(codec),
                codec,
                $"No defaults for {codec}"
            ),
        };
}
