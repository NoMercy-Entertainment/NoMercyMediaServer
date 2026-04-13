namespace NoMercy.Encoder.Subtitles;

public static class SubtitleClassifier
{
    private static readonly HashSet<string> TextCodecs =
    [
        "srt",
        "subrip",
        "ass",
        "ssa",
        "webvtt",
        "mov_text",
        "text",
    ];

    private static readonly HashSet<string> BitmapCodecs =
    [
        "hdmv_pgs_subtitle",
        "dvd_subtitle",
        "dvb_subtitle",
    ];

    public static bool IsTextBased(string codec) => TextCodecs.Contains(codec.ToLowerInvariant());

    public static bool IsBitmapBased(string codec) =>
        BitmapCodecs.Contains(codec.ToLowerInvariant());
}
