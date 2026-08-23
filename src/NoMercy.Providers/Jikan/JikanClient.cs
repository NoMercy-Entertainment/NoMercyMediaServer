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

using Newtonsoft.Json;
using NoMercy.Providers.Jikan.Models;

namespace NoMercy.Providers.Jikan;

public static class JikanClient
{
    public static async Task<JikanAnime?> SearchAsync(HttpClient client, string title, int? year)
    {
        string query = Uri.EscapeDataString(title);
        string url = year is not null
            ? $"anime?q={query}&start_date={year}-01-01&limit=5"
            : $"anime?q={query}&limit=5";

        using HttpResponseMessage response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        string body = await response.Content.ReadAsStringAsync();
        JikanSearchResponse? parsed = JsonConvert.DeserializeObject<JikanSearchResponse>(body);

        return parsed?.Data.FirstOrDefault();
    }

    // /anime search is a known-unreliable Jikan endpoint (jikan-me/jikan-rest#610):
    // it hard-fails with a 504 "MyAnimeList may be down" even while MAL itself
    // and Jikan's own /anime/{id} lookup both respond normally - verified live,
    // 8/8 search calls 504 in ~40ms (too fast to be a real MAL round-trip,
    // reads as a short-circuited/cached failure) while /anime/{id} returns 200.
    // Call this instead of SearchAsync whenever a MAL id is already known (from
    // AniList's idMal cross-reference), since it hits a path that actually works.
    public static async Task<JikanAnime?> GetByIdAsync(HttpClient client, int malId)
    {
        using HttpResponseMessage response = await client.GetAsync($"anime/{malId}");
        if (!response.IsSuccessStatusCode)
            return null;

        string body = await response.Content.ReadAsStringAsync();
        JikanAnimeResponse? parsed = JsonConvert.DeserializeObject<JikanAnimeResponse>(body);

        return parsed?.Data;
    }
}
