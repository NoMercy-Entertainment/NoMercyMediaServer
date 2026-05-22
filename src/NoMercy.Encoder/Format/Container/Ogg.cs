using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class Ogg : BaseContainer
{
    public override ContainerDto ContainerDto =>
        AvailableContainers.First(c => c.Name == AudioContainers.Ogg);

    public Ogg()
    {
        SetContainer(AudioContainers.Ogg);
        AddCustomArgument("-f", AudioFormats.Ogg);
    }

    public override CodecDto[] AvailableCodecs =>
        [AudioCodecs.LibOpus, AudioCodecs.LibVorbis, AudioCodecs.Flac];

    public override CodecDto[] AvailableAudioCodecs =>
        [AudioCodecs.LibOpus, AudioCodecs.LibVorbis, AudioCodecs.Flac];

    public override bool SupportsSpriteStream => false;

    public override bool SupportsFontsExtraction => false;

    public override Ogg ApplyFlags()
    {
        base.ApplyFlags();
        return this;
    }
}
