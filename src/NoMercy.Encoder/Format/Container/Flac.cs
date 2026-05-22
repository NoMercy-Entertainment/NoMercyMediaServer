using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class Flac : BaseContainer
{
    public override ContainerDto ContainerDto =>
        AvailableContainers.First(c => c.Name == AudioContainers.Flac);

    public Flac()
    {
        SetContainer(AudioContainers.Flac);
        AddCustomArgument("-f", AudioFormats.Flac);
    }

    public override CodecDto[] AvailableCodecs => [AudioCodecs.Flac];

    public override bool SupportsSpriteStream => false;

    public override bool SupportsFontsExtraction => false;

    public override Flac ApplyFlags()
    {
        base.ApplyFlags();

        return this;
    }
}
