using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Video;

// https://trac.ffmpeg.org/wiki/Encode/AV1

public class AV1 : BaseVideo
{
    protected internal override bool BFramesSupport => true;
    protected internal override int Modulus => 2;
    protected internal int Passes { get; set; } = 2;
    protected internal override int[] CrfRange => [0, 63];

    public AV1(string videoCodec = "librav1e")
    {
        try
        {
            SetVideoCodec(videoCodec);
        }
        catch (Exception e)
        {
            SetVideoCodec(VideoCodecs.Av1.Value);
        }
    }

    protected override CodecDto[] AvailableCodecs =>
    [
        VideoCodecs.Av1,
        VideoCodecs.Av1Nvenc,
        // VideoCodecs.Av1Qsv,
        // VideoCodecs.Av1Amf,
        // VideoCodecs.Av1Videotoolbox
    ];

    protected internal override string[] AvailableContainers =>
    [
        VideoContainers.Mkv,
        VideoContainers.Mp4,
        VideoContainers.Hls
    ];

    public override string[] AvailablePresets
    {
        get
        {
            if (VideoCodecs.Av1Nvenc.Value == VideoCodec.Value)
            {
                return
                [
                    VideoPresets.Default, VideoPresets.Slow, VideoPresets.Medium, VideoPresets.Fast,
                    VideoPresets.Hp, VideoPresets.Hq, VideoPresets.Ll, VideoPresets.Llhq, VideoPresets.Llhp,
                    VideoPresets.Lossless,
                    VideoPresets.P1, VideoPresets.P2, VideoPresets.P3, VideoPresets.P4, VideoPresets.P5,
                    VideoPresets.P6, VideoPresets.P7
                ];
            }
            else if (VideoCodecs.Av1Amf.Value == VideoCodec.Value)
            {
            }
            else if (VideoCodecs.Av1Qsv.Value == VideoCodec.Value)
            {
            }
            else if (VideoCodecs.Av1Videotoolbox.Value == VideoCodec.Value)
            {
            }

            return
            [
                VideoPresets.UltraFast, VideoPresets.SuperFast, VideoPresets.VeryFast,
                VideoPresets.Faster, VideoPresets.Fast, VideoPresets.Medium,
                VideoPresets.Slow, VideoPresets.Slower, VideoPresets.VerySlow,
                VideoPresets.Placebo
            ];
        }
    }

    public override string[] AvailableProfiles
    {
        get
        {
            if (VideoCodecs.Av1Nvenc.Value == VideoCodec.Value)
            {
                return
                [
                    VideoProfiles.Baseline, VideoProfiles.Main, VideoProfiles.High,
                    VideoProfiles.High10, VideoProfiles.High422, VideoProfiles.High444P
                ];
            }
            else if (VideoCodecs.Av1Amf.Value == VideoCodec.Value)
            {
            }
            else if (VideoCodecs.Av1Qsv.Value == VideoCodec.Value)
            {
            }
            else if (VideoCodecs.Av1Videotoolbox.Value == VideoCodec.Value)
            {
            }

            return
            [
                VideoProfiles.Baseline, VideoProfiles.Main, VideoProfiles.High,
                VideoProfiles.High10, VideoProfiles.High444P
            ];
        }
    }

    public override string[] AvailableTune
    {
        get
        {
            if (VideoCodecs.Av1Nvenc.Value == VideoCodec.Value)
            {
                return
                [
                    VideoTunes.Hq, VideoTunes.Li,
                    VideoTunes.Ull, VideoTunes.Lossless
                ];
            }
            else if (VideoCodecs.Av1Amf.Value == VideoCodec.Value)
            {
            }
            else if (VideoCodecs.Av1Qsv.Value == VideoCodec.Value)
            {
            }
            else if (VideoCodecs.Av1Videotoolbox.Value == VideoCodec.Value)
            {
            }
            
            return
            [
                VideoTunes.Film, VideoTunes.Animation,
                VideoTunes.Grain, VideoTunes.StillImage,
                VideoTunes.Fastdecode, VideoTunes.Zerolatency,
                VideoTunes.Psnr, VideoTunes.Ssim
            ];
        }
    }

    public override string[] AvailableColorSpaces =>
    [
        ColorSpaces.Yuv420P, ColorSpaces.Yuv420P10Le,
        ColorSpaces.Yuv422P, ColorSpaces.Yuv444P,
    ];

    public override string[] AvailableLevels =>
    [
        "auto",
        "1", "1.0", "1b", "1.0b", "1.1", "1.2", "1.3",
        "2", "2.0", "2.1", "2.2",
        "3", "3.0", "3.1", "3.2",
        "4", "4.0", "4.1", "4.2",
        "5", "5.0", "5.1", "5.2",
        "6.0", "6.1", "6.2"
    ];

    public AV1 SetPasses(int passes)
    {
        Passes = passes;
        return this;
    }

    public override int GetPasses()
    {
        return 0 == Bitrate ? 1 : Passes;
    }
}