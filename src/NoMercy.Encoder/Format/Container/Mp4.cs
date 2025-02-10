using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class Mp4 : BaseContainer
{
    public override ContainerDto ContainerDto => AvailableContainers.First(c => c.Name == VideoContainers.Mp4);

    public Mp4()
    {
        SetContainer(VideoContainers.Mp4);
        AddCustomArgument("-f", VideoFormats.Mp4);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        VideoCodecs.H264, VideoCodecs.H264Nvenc,
        VideoCodecs.H265, VideoCodecs.H265Nvenc,
        VideoCodecs.Vp9, VideoCodecs.Vp9Nvenc
    ];

    public override CodecDto[] AvailableVideoCodecs => [
        VideoCodecs.H264, VideoCodecs.H264Nvenc,
        VideoCodecs.H265, VideoCodecs.H265Nvenc,
    ];

    public override CodecDto[] AvailableAudioCodecs => [
        AudioCodecs.Mp3
    ];

    public override CodecDto[] AvailableSubtitleCodecs => [
        SubtitleCodecs.Webvtt, SubtitleCodecs.Srt, SubtitleCodecs.Ass,
        SubtitleCodecs.Copy
    ];

    public override Mp4 ApplyFlags()
    {
        base.ApplyFlags();
        AddCustomArgument("-bsf:v", "h264_mp4toannexb");
        AddCustomArgument("-use_wallclock_as_timestamps", 1);
        return this;
    }
}