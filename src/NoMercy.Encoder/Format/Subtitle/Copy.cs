using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Subtitle;

public class Copy : BaseSubtitle
{
    public Copy(string subtitleCodec = "copy")
    {
        SetSubtitleCodec(subtitleCodec);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        SubtitleCodecs.Copy
    ];

    protected override string[] AvailableContainers =>
    [
        SubtitleContainers.WebVtt, SubtitleContainers.Srt,
        SubtitleContainers.Ass
    ];
}