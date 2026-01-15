namespace NoMercy.Encoder.Format.Rules;

public static class SubtitleCodecs
{
    public static readonly Classes.CodecDto Ass = new()
    {
        Name = "Ass",
        Value = "ass",
        SimpleValue = "ass",
        IsDefault = false
    };

    public static readonly Classes.CodecDto Srt = new()
    {
        Name = "SubRip",
        Value = "srt",
        SimpleValue = "srt",
        IsDefault = false
    };

    public static readonly Classes.CodecDto Webvtt = new()
    {
        Name = "WebVTT",
        Value = "webvtt",
        SimpleValue = "vtt",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Copy = new()
    {
        Name = "Copy",
        Value = "copy",
        SimpleValue = "copy",
        IsDefault = false
    };
}