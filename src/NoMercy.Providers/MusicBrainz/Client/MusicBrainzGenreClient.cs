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

// ReSharper disable All

using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzGenreClient : MusicBrainzBaseClient
{
    public MusicBrainzGenreClient()
        : base() { }

    public Task<MusicBrainzAllGenres?> FirstPage() =>
        Get<MusicBrainzAllGenres>(
            url: "genre/all",
            query: new Dictionary<string, string?>
            {
                [key: "limit"] = "100",
                [key: "offset"] = "0",
                [key: "fmt"] = "json",
            }
        );

    public async Task<List<MusicBrainzGenre>> RemainingPages(MusicBrainzAllGenres firstPage)
    {
        List<MusicBrainzGenre> genres = [];
        int pageSize = firstPage.Genres.Length;

        if (pageSize == 0)
            return genres;

        for (long offset = pageSize; offset < firstPage.GenreCount; offset += pageSize)
        {
            MusicBrainzAllGenres? page = await Get<MusicBrainzAllGenres>(
                url: "genre/all",
                query: new Dictionary<string, string?>
                {
                    [key: "limit"] = pageSize.ToString(),
                    [key: "offset"] = offset.ToString(),
                    [key: "fmt"] = "json",
                }
            );

            if (page is null)
                continue;

            genres.AddRange(collection: page.Genres);
        }

        return genres;
    }

    public async Task<List<MusicBrainzGenre>> All()
    {
        MusicBrainzAllGenres? firstPage = await FirstPage();
        if (firstPage is null)
            return [];

        List<MusicBrainzGenre> genres = [.. firstPage.Genres];
        genres.AddRange(collection: await RemainingPages(firstPage: firstPage));
        return genres;
    }

    public async Task<MusicBrainzGenre?> SearchGenre(string query)
    {
        MusicBrainzAllGenres? data = await Get<MusicBrainzAllGenres>(
            url: "genre",
            query: new Dictionary<string, string?> { [key: "query"] = query, [key: "fmt"] = "json" }
        );

        return data?.Genres.FirstOrDefault();
    }
}
