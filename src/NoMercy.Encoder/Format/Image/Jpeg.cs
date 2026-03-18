using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Image;

public class Jpeg : BaseImage
{
    public Jpeg()
    {
        SetImageCodec(ImageCodecs.Jpeg.Value);
        AddCustomArgument("-f", ImageCodecs.Jpeg.Value);
    }

    private protected override CodecDto[] AvailableCodecs =>
    [
        ImageCodecs.Jpeg
    ];

    private protected override string[] AvailableContainers =>
    [
        "jpeg"
    ];

    private protected override string[] AvailablePresets => [];

    private protected override string[] AvailableProfiles => [];
}