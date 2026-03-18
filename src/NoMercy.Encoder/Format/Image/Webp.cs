using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Image;

public class Webp : BaseImage
{
    protected Webp(string imageCodec = "webp")
    {
        SetImageCodec(imageCodec);
        AddCustomArgument("-f", imageCodec);
    }

    private protected override CodecDto[] AvailableCodecs =>
    [
        ImageCodecs.Webp
    ];

    private protected override string[] AvailableContainers =>
    [
        "webp"
    ];

    private protected override string[] AvailableFormats =>
    [
        "bgra", "yuv420p", "yuva420p"
    ];

    private protected override string[] AvailablePresets => [];

    private protected override string[] AvailableProfiles => [];
}