using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Subtitle;

public class Srt : BaseSubtitle
{
    public Srt(string subtitleCodec = "srt")
    {
        SetSubtitleCodec(subtitleCodec);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        SubtitleCodecs.Srt
    ];

    protected override string[] AvailableContainers =>
    [
        SubtitleContainers.Srt
    ];
}