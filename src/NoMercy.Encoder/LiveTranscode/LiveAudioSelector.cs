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

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Chooses which source audio stream a live session maps by default. A viewer
/// wants the audio in their configured language, not whichever track the file
/// happens to list first — an anime that defaults to Japanese should still open
/// in English for a library configured that way. Preference wins over the file's
/// own default flag; the default flag is the fallback; the first stream is the
/// last resort.
/// </summary>
public static class LiveAudioSelector
{
    // ISO 639-1 (2-letter, how libraries store a language) → the ISO 639-2 codes
    // ffprobe emits in a stream's language tag. Both the bibliographic (/B: ger,
    // fre, dut) and terminological (/T: deu, fra, nld) forms are listed because
    // muxers use either. Covers the languages real libraries are configured with;
    // anything outside the map falls back to a leading-two-letter compare.
    private static readonly Dictionary<string, string[]> Iso6392ByIso6391 = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["en"] = ["eng"],
        ["nl"] = ["nld", "dut"],
        ["de"] = ["deu", "ger"],
        ["fr"] = ["fra", "fre"],
        ["es"] = ["spa"],
        ["it"] = ["ita"],
        ["pt"] = ["por"],
        ["ru"] = ["rus"],
        ["ja"] = ["jpn"],
        ["ko"] = ["kor"],
        ["zh"] = ["zho", "chi"],
        ["ar"] = ["ara"],
        ["hi"] = ["hin"],
        ["sv"] = ["swe"],
        ["no"] = ["nor"],
        ["da"] = ["dan"],
        ["fi"] = ["fin"],
        ["pl"] = ["pol"],
        ["tr"] = ["tur"],
        ["cs"] = ["ces", "cze"],
        ["el"] = ["ell", "gre"],
        ["he"] = ["heb"],
        ["hu"] = ["hun"],
        ["th"] = ["tha"],
        ["uk"] = ["ukr"],
        ["vi"] = ["vie"],
    };

    /// <summary>
    /// Returns the zero-based index AMONG AUDIO STREAMS to map (<c>0:a:N</c>).
    /// Scans <paramref name="preferredIso6391"/> in order and returns the first
    /// audio stream in that language; then the source's default-flagged stream;
    /// then the first stream. Never throws — returns 0 for an empty stream list.
    /// </summary>
    public static int Select(
        IReadOnlyList<AudioStreamInfo> audioStreams,
        IReadOnlyList<string> preferredIso6391
    )
    {
        if (audioStreams.Count == 0)
            return 0;

        foreach (string preferred in preferredIso6391)
        {
            for (int index = 0; index < audioStreams.Count; index++)
            {
                if (LanguageMatches(audioStreams[index].Language, preferred))
                    return index;
            }
        }

        for (int index = 0; index < audioStreams.Count; index++)
        {
            if (audioStreams[index].IsDefault)
                return index;
        }

        return 0;
    }

    /// <summary>
    /// True when a stream/rendition language tag matches a viewer's preferred
    /// ISO 639-1 code, accounting for the 639-2/B and /T forms muxers emit and a
    /// leading-two-letter fallback. Shared by the master-playlist builder to pick
    /// which pre-encoded audio rendition opens by default.
    /// </summary>
    public static bool LanguageMatches(string? streamLanguage, string preferredIso6391)
    {
        if (
            string.IsNullOrWhiteSpace(streamLanguage) || string.IsNullOrWhiteSpace(preferredIso6391)
        )
            return false;

        string stream = streamLanguage.Trim().ToLowerInvariant();
        string preferred = preferredIso6391.Trim().ToLowerInvariant();

        if (stream == preferred)
            return true;

        if (Iso6392ByIso6391.TryGetValue(preferred, out string[]? forms) && forms.Contains(stream))
            return true;

        return stream.Length >= 2 && preferred.Length >= 2 && stream[..2] == preferred[..2];
    }
}
