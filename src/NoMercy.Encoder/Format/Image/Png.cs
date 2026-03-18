using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Image;

public class Png : BaseImage
{
    public Png(string imageCodec = "png")
    {
        SetImageCodec(imageCodec);
        AddCustomArgument("-f", imageCodec);
        throw new NotImplementedException("Png is not implemented yet");
    }

    private protected override CodecDto[] AvailableCodecs =>
    [
        ImageCodecs.Png
    ];

    private protected override string[] AvailableContainers =>
    [
        "png"
    ];

    private protected override string[] AvailablePresets => [];

    private protected override string[] AvailableProfiles => [];
}