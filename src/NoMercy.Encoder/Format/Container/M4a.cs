using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class M4a : BaseContainer
{
    public override ContainerDto ContainerDto =>
        AvailableContainers.First(c => c.Name == AudioContainers.M4A);

    public M4a()
    {
        SetContainer(AudioContainers.M4A);
        AddCustomArgument("-f", AudioFormats.M4A);
    }

    public override CodecDto[] AvailableCodecs => [AudioCodecs.Aac];

    public override CodecDto[] AvailableAudioCodecs => [AudioCodecs.Aac];

    public override bool SupportsSpriteStream => false;

    public override bool SupportsFontsExtraction => false;

    public override M4a ApplyFlags()
    {
        base.ApplyFlags();
        return this;
    }
}
