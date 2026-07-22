// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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

    // Extensions the subtitle extraction pass writes for a bitmap track. These
    // carry picture data (or, for .mks, a Matroska container wrapping it) and
    // cannot be served to a player as a text sidecar — only the OCR pass's .vtt
    // sibling can. The extraction pass writes .mks, so a list of just sup/vob
    // let every bitmap track through and it was published as a track the player
    // could list but never render.
    private static readonly HashSet<string> BitmapSidecarExtensions = ["mks", "sup", "idx", "vob"];

    public static bool IsTextBased(string codec) => TextCodecs.Contains(item: codec.ToLowerInvariant());

    public static bool IsBitmapBased(string codec) =>
        BitmapCodecs.Contains(item: codec.ToLowerInvariant());

    /// <summary>
    /// True when a sidecar file's extension denotes a bitmap subtitle. The single
    /// source of truth for callers that only have a filename to go on — the
    /// library scan and the playback track list both classify that way.
    /// </summary>
    public static bool IsBitmapSidecarExtension(string extension) =>
        BitmapSidecarExtensions.Contains(item: extension.TrimStart(trimChar: '.').ToLowerInvariant());

    // Title takes priority over disposition flags so signs/songs and SDH
    // tracks land in the right slot even when the muxer mis-flagged them.
    // Single-stream form: returns "full" when no signal classifies the
    // stream as sign/sdh. Use ResolveVariants for multi-stream context where
    // a second un-classified stream in the same language should become "alt".
    public static string ResolveVariant(SubtitleStreamInfo stream)
    {
        return PreClassify(stream: stream) ?? "full";
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
            comparer: StringComparer.OrdinalIgnoreCase
        );

        for (int i = 0; i < streams.Count; i++)
        {
            string? preClassified = PreClassify(stream: streams[index: i]);
            if (preClassified is not null)
            {
                variants[i] = preClassified;
                continue;
            }

            string language = streams[index: i].Language ?? "und";
            if (!unclassifiedByLanguage.TryGetValue(key: language, value: out List<int>? indices))
                unclassifiedByLanguage[key: language] = indices = [];
            indices.Add(item: i);
        }

        foreach (List<int> indices in unclassifiedByLanguage.Values)
        {
            // Prefer the default-flagged stream as "full"; otherwise the
            // first un-classified stream in source order.
            int fullIndex = indices.FirstOrDefault(predicate: i => streams[index: i].IsDefault, defaultValue: -1);
            if (fullIndex < 0)
                fullIndex = indices[index: 0];

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

        if (title.Contains(value: "s&s") || title.Contains(value: "sign") || title.Contains(value: "song"))
            return "sign";

        if (title.Contains(value: "sdh") || title.Contains(value: "hearing"))
            return "sdh";

        if (stream.IsForced)
            return "sign";

        return null;
    }
}
