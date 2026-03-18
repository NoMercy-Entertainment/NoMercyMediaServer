using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class Ass : BaseContainer
{
    public Ass()
    {
        SetContainer(SubtitleContainers.Ass);
        AddCustomArgument("-f", SubtitleFormats.Ass);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        SubtitleCodecs.Ass
    ];
}