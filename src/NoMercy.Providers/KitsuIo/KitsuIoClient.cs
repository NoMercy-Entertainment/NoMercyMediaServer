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

using System.Text.RegularExpressions;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.KitsuIo;

public static partial class KitsuIoClient
{
    private static readonly Uri BaseUrl = new("https://kitsu.io/api/edge/");

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespace();

    // filter[text] carries the show's raw title verbatim — any space (i.e. almost
    // every real title) breaks the query string, and Kitsu returns no candidates at
    // all. Unescaped, this made every multi-word title classify as not-anime.
    //
    // A leading/standalone "-" also breaks Kitsu's own search: querying the exact
    // local title "Re:ZERO -Starting Life in Another World-" returns nothing,
    // while the same query with the dash-delimited subtitle stripped ("Re:ZERO")
    // finds the show immediately — Kitsu's search backend reads a bare "-word" as
    // an exclusion token. Sanitize() keeps "-" and "." (needed elsewhere, e.g.
    // hyphenated titles and years), so normalize them to spaces here, specifically
    // for what we send Kitsu and what we compare its answer against.
    private static string NormalizeForMatch(string title) =>
        CollapseWhitespace()
            .Replace(title.Sanitize().Replace('-', ' ').Replace('.', ' '), " ")
            .Trim();

    // Word SET, not sequence: Kitsu's English localizers don't preserve word
    // order either — reproduced live, Kitsu answers "SAINT SEIYA: Knights of
    // the Zodiac" with en: "Knights of the Zodiac: Saint Seiya", same words,
    // reordered. A prefix/substring compare can never match that; requiring
    // every word of the search title to appear somewhere in the candidate does,
    // while still catching the year-suffix case (the suffix is simply an extra
    // word neither side requires the other to also have).
    private static string[] WordsForMatch(string title) =>
        NormalizeForMatch(title).ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    internal static string BuildQuery(string title, int year) =>
        $"anime?filter[text]={Uri.EscapeDataString(NormalizeForMatch(title))}&filter[year]={year}";

    /// <summary>
    /// Whether Kitsu confirms this title as anime. Null means the lookup itself
    /// failed — a network error, or a non-2xx response (Kitsu rate-limits hard, and
    /// a burst of lookups WILL trip it) — and is a real "we don't know", never
    /// collapsed into false. A caller treating null the same as false would move an
    /// already-correctly-filed show out of its library on every transient hiccup,
    /// which is exactly what silently happened here before this returned a tri-state.
    /// </summary>
    public static async Task<bool?> IsAnime(string title, int year)
    {
        HttpClient client = HttpClientProvider.CreateClient(HttpClientNames.KitsuIo);
        client.BaseAddress ??= BaseUrl;

        try
        {
            using HttpResponseMessage response = await client.GetAsync(BuildQuery(title, year));

            if (!response.IsSuccessStatusCode)
            {
                Logger.AniDb(
                    $"Kitsu lookup for '{title}' ({year}) failed: {(int)response.StatusCode} {response.StatusCode}",
                    LogEventLevel.Warning
                );
                return null;
            }

            string content = await response.Content.ReadAsStringAsync();
            KitsuAnime? anime = content.FromJson<KitsuAnime>();

            // FromJson swallows its own parse errors and returns null rather than
            // throwing, so an unparsable 200 body reads identically to "no
            // candidates" unless checked explicitly here too.
            if (anime is null)
            {
                Logger.AniDb(
                    $"Kitsu lookup for '{title}' ({year}) returned an unparsable body",
                    LogEventLevel.Warning
                );
                return null;
            }

            // Byte-exact equality against Kitsu's title fields is too fragile to
            // trust: Kitsu's canonical en/en_jp title commonly carries a
            // disambiguating "(YYYY)" suffix the local title never has ("Fruits
            // Basket" vs "Fruits Basket (2019)"), Kitsu's editors use typographic
            // punctuation ("Journey's End" with a curly apostrophe) where the
            // local title has a plain one, the dash/colon used to set off a
            // subtitle is inconsistent in EITHER direction ("Nichijou: My
            // Ordinary Life" locally vs Kitsu's "Nichijou - My Ordinary Life", and
            // the reverse for other shows), and the words themselves can come back
            // reordered ("SAINT SEIYA: Knights of the Zodiac" vs Kitsu's "Knights
            // of the Zodiac: Saint Seiya") — all reproduced live, all fail an
            // exact or prefix match. Comparing word SETS (every search-title word
            // present somewhere in the candidate) survives all four.
            string[] titleWords = WordsForMatch(title);

            foreach (Data data in anime.Data)
            {
                IEnumerable<string> candidateTitles = new[]
                {
                    data.Attributes.Titles.En,
                    data.Attributes.Titles.EnUs,
                    data.Attributes.Titles.EnJp,
                    data.Attributes.Titles.JaJp,
                    data.Attributes.Titles.ThTh,
                }
                    .Concat(data.Attributes.AbbreviatedTitles)
                    .Where(candidateTitle => !string.IsNullOrEmpty(candidateTitle))!;

                if (
                    candidateTitles.Any(candidateTitle =>
                    {
                        HashSet<string> candidateWords = WordsForMatch(candidateTitle).ToHashSet();
                        return titleWords.Length > 0 && titleWords.All(candidateWords.Contains);
                    })
                )
                    return true;
            }

            return false;
        }
        catch (Exception e)
        {
            Logger.AniDb(e.Message, LogEventLevel.Fatal);
            return null;
        }
    }
}
