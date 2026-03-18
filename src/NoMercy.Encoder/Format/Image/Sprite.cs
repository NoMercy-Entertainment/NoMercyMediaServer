using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Image;

public class Sprite : BaseImage
{
    public Sprite()
    {
        SetImageCodec(ImageCodecs.Jpeg.Value);
        FrameRate = 10;
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