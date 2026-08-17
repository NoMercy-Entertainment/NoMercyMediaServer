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

using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.KitsuIo;

public static class KitsuIoClient
{
    private static readonly Uri BaseUrl = new("https://kitsu.io/api/edge/");

    // filter[text] carries the show's raw title verbatim — any space (i.e. almost
    // every real title) breaks the query string, and Kitsu returns no candidates at
    // all. Unescaped, this made every multi-word title classify as not-anime.
    internal static string BuildQuery(string title, int year) =>
        $"anime?filter[text]={Uri.EscapeDataString(title)}&filter[year]={year}";

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

            foreach (Data data in anime.Data)
                if (
                    data.Attributes.Titles.En?.Equals(
                        title,
                        StringComparison.CurrentCultureIgnoreCase
                    ) == true
                    || data.Attributes.Titles.EnJp?.Equals(
                        title,
                        StringComparison.CurrentCultureIgnoreCase
                    ) == true
                    || data.Attributes.Titles.JaJp?.Equals(
                        title,
                        StringComparison.CurrentCultureIgnoreCase
                    ) == true
                    || data.Attributes.Titles.ThTh?.Equals(
                        title,
                        StringComparison.CurrentCultureIgnoreCase
                    ) == true
                    || data.Attributes.AbbreviatedTitles.Any(abbreviatedTitle =>
                        abbreviatedTitle.Equals(title, StringComparison.CurrentCultureIgnoreCase)
                    )
                )
                    return true;

            return false;
        }
        catch (Exception e)
        {
            Logger.AniDb(e.Message, LogEventLevel.Fatal);
            return null;
        }
    }
}
