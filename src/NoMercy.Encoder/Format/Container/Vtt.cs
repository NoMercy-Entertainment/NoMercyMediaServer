using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class Vtt : BaseContainer
{
    public Vtt()
    {
        SetContainer(SubtitleContainers.WebVtt);
        AddCustomArgument("-f", SubtitleFormats.WebVtt);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        SubtitleCodecs.Webvtt
    ];
}