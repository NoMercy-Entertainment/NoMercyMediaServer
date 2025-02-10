using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class Mp3 : BaseContainer
{
    public override ContainerDto ContainerDto => AvailableContainers.First(c => c.Name == AudioContainers.Mp3);

    public Mp3()
    {
        SetContainer(AudioContainers.Mp3);
        AddCustomArgument("-f", AudioFormats.Mp3);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        AudioCodecs.Mp3,
    ];

    public override Mp3 ApplyFlags()
    {
        base.ApplyFlags();

        return this;
    }
}