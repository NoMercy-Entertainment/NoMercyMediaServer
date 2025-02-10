using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class Srt : BaseContainer
{
    public Srt()
    {
        SetContainer(SubtitleContainers.Srt);
        AddCustomArgument("-f", SubtitleFormats.Srt);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        SubtitleCodecs.Srt
    ];
}