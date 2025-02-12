using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Video;

// https://trac.ffmpeg.org/wiki/Encode/VP9

public class Vp9 : BaseVideo
{
    protected internal override bool BFramesSupport => true;
    protected internal int Passes { get; set; } = 2;
    protected internal override int[] CrfRange => [0, 63];

    public Vp9(string videoCodec = "vp9")
    {
        try
        {
            SetVideoCodec(videoCodec);
        }
        catch (Exception e)
        {
            SetVideoCodec(VideoCodecs.Vp9.Value);
        }
    }

    protected override CodecDto[] AvailableCodecs =>
    [
        VideoCodecs.Vp9,
        VideoCodecs.Vp9Nvenc,
        // VideoCodecs.Vp9Qsv,
        // VideoCodecs.Vp9Amf,
        // VideoCodecs.Vp9Videotoolbox
    ];

    protected internal override string[] AvailableContainers =>
    [
        VideoContainers.Mkv, VideoContainers.Webm,
        VideoContainers.Flv, VideoContainers.Hls
    ];

    public override string[] AvailablePresets =>
    [
        VideoPresets.VeryFast, VideoPresets.Faster, VideoPresets.Fast,
        VideoPresets.Medium,
        VideoPresets.Slow, VideoPresets.Slower, VideoPresets.VerySlow
    ];

    public override string[] AvailableProfiles =>
    [
        VideoProfiles.Unknown, VideoProfiles.Profile0, VideoProfiles.Profile1,
        VideoProfiles.Profile2, VideoProfiles.Profile3
    ];

    public override string[] AvailableColorSpaces =>
    [
        ColorSpaces.Yuv420P, ColorSpaces.Yuv420P10Le,
        ColorSpaces.Yuv422P, ColorSpaces.Yuv444P,
    ];

    public override string[] AvailableTune =>
    [
        VideoTunes.Hq, VideoTunes.Li,
        VideoTunes.Ull, VideoTunes.Lossless
    ];

    public override string[] AvailableLevels => [];

    public override int GetPasses()
    {
        return 0 == Bitrate ? 1 : Passes;
    }
}