using NoMercy.Encoder.Analysis;

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

    // Title takes priority over disposition flags so signs/songs and SDH
    // tracks land in the right slot even when the muxer mis-flagged them.
    public static string ResolveVariant(SubtitleStreamInfo stream)
    {
        string title = stream.Title?.ToLowerInvariant() ?? "";

        if (title.Contains("s&s") || title.Contains("sign") || title.Contains("song"))
            return "sign";

        if (title.Contains("sdh") || title.Contains("hearing"))
            return "sdh";

        if (stream.IsForced)
            return "sign";

        if (stream.IsDefault)
            return "full";

        return "alt";
    }
}
