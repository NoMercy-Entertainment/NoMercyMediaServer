using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Image;

public class Gif : BaseImage
{
    public Gif(string imageCodec = "gif")
    {
        SetImageCodec(imageCodec);
        AddCustomArgument("-f", "gif");
        throw new NotImplementedException("Gif is not implemented yet");
    }

    private protected override CodecDto[] AvailableCodecs =>
    [
        ImageCodecs.Gif
    ];

    private protected override string[] AvailableContainers =>
    [
        "gif"
    ];

    private protected override string[] AvailablePresets => [];

    private protected override string[] AvailableProfiles => [];
}