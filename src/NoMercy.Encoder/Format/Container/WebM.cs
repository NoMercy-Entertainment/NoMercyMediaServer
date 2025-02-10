using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class WebM : BaseContainer
{
    public override ContainerDto ContainerDto => AvailableContainers.First(c => c.Name == VideoContainers.Webm);

    public WebM()
    {
        SetContainer(VideoContainers.Webm);
        AddCustomArgument("-f", VideoFormats.Webm);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        VideoCodecs.Vp9, VideoCodecs.Vp9Nvenc
    ];

    public override CodecDto[] AvailableVideoCodecs => [
        VideoCodecs.Vp9, VideoCodecs.Vp9Nvenc,
    ];

    public override CodecDto[] AvailableAudioCodecs => [
        AudioCodecs.Opus, AudioCodecs.Vorbis,
    ];

    public override CodecDto[] AvailableSubtitleCodecs => [
        SubtitleCodecs.Webvtt, SubtitleCodecs.Srt, SubtitleCodecs.Ass,
        SubtitleCodecs.Copy
    ];
}