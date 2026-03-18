using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Subtitle;

public class Ass : BaseSubtitle
{
    public Ass(string subtitleCodec = "ass")
    {
        SetSubtitleCodec(subtitleCodec);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        SubtitleCodecs.Ass
    ];

    protected override string[] AvailableContainers =>
    [
        SubtitleContainers.Ass
    ];
}