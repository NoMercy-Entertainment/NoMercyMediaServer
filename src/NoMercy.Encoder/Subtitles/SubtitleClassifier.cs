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

    // Includes both canonical FFmpeg codec names (hdmv_pgs_subtitle / dvd_subtitle /
    // dvb_subtitle) and the short aliases NoMercy normalises to internally
    // (pgs / vobsub). Different code paths see different forms depending on
    // whether the codec came from ffprobe (canonical) or from a normalized
    // reconstruction-manifest field (short), so a permissive matcher keeps
    // the bitmap-vs-text classification consistent across both.
    private static readonly HashSet<string> BitmapCodecs =
    [
        "hdmv_pgs_subtitle",
        "pgs",
        "dvd_subtitle",
        "vobsub",
        "dvb_subtitle",
    ];

    public static bool IsTextBased(string codec) => TextCodecs.Contains(codec.ToLowerInvariant());

    public static bool IsBitmapBased(string codec) =>
        BitmapCodecs.Contains(codec.ToLowerInvariant());

    // Title takes priority over disposition flags so signs/songs and SDH
    // tracks land in the right slot even when the muxer mis-flagged them.
    // Single-stream form: returns "full" when no signal classifies the
    // stream as sign/sdh. Use ResolveVariants for multi-stream context where
    // a second un-classified stream in the same language should become "alt".
    public static string ResolveVariant(SubtitleStreamInfo stream)
    {
        return PreClassify(stream) ?? "full";
    }

    /// <summary>
    /// Resolves variants for a full set of subtitle streams at once. Use
    /// this whenever multiple streams are in scope (encoding plan, full
    /// media probe) — it groups un-classified streams by language and
    /// promotes the first one (preferring <see cref="SubtitleStreamInfo.IsDefault"/>)
    /// to "full". Remaining un-classified streams in that language become
    /// "alt". The single-stream <see cref="ResolveVariant"/> overload has
    /// no peer context and would mark every stream "full" — which collides
    /// when a source carries multiple regular tracks per language.
    /// </summary>
    public static IReadOnlyList<string> ResolveVariants(IReadOnlyList<SubtitleStreamInfo> streams)
    {
        string[] variants = new string[streams.Count];
        Dictionary<string, List<int>> unclassifiedByLanguage = new(
            StringComparer.OrdinalIgnoreCase
        );

        for (int i = 0; i < streams.Count; i++)
        {
            string? preClassified = PreClassify(streams[i]);
            if (preClassified is not null)
            {
                variants[i] = preClassified;
                continue;
            }

            string language = streams[i].Language ?? "und";
            if (!unclassifiedByLanguage.TryGetValue(language, out List<int>? indices))
                unclassifiedByLanguage[language] = indices = [];
            indices.Add(i);
        }

        foreach (List<int> indices in unclassifiedByLanguage.Values)
        {
            // Prefer the default-flagged stream as "full"; otherwise the
            // first un-classified stream in source order.
            int fullIndex = indices.FirstOrDefault(i => streams[i].IsDefault, -1);
            if (fullIndex < 0)
                fullIndex = indices[0];

            foreach (int i in indices)
                variants[i] = i == fullIndex ? "full" : "alt";
        }

        return variants;
    }

    /// <summary>
    /// First-pass classification using only signals on the stream itself.
    /// Returns null when no signal classifies the stream — caller decides
    /// whether to default to "full" (single-stream context) or defer to
    /// the per-language pass for "full" vs "alt" disambiguation.
    /// </summary>
    private static string? PreClassify(SubtitleStreamInfo stream)
    {
        string title = stream.Title?.ToLowerInvariant() ?? "";

        if (title.Contains("s&s") || title.Contains("sign") || title.Contains("song"))
            return "sign";

        if (title.Contains("sdh") || title.Contains("hearing"))
            return "sdh";

        if (stream.IsForced)
            return "sign";

        return null;
    }
}
