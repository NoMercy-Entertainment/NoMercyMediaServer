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
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.KitsuIo;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

public class MediaTypeClassifier : IMediaTypeClassifier
{
    public Task<string?> ClassifyAsync(TmdbTvShowAppends show)
    {
        return ClassifyAsync(show.Name, show.FirstAirDate.ParseYear(), show.OriginCountry);
    }

    public async Task<string?> ClassifyAsync(string name, int? year, string[]? originCountry = null)
    {
        bool? isAnime = await KitsuIoClient.IsAnime(name, year ?? 0);

        // Kitsu's community catalogue lists non-Japanese productions that got a
        // fan-run entry (Avatar: The Last Airbender, The Legend of Korra, The
        // Dragon Prince all have real Kitsu results), so a title match alone
        // isn't enough — reproduced live: all three matched by title and would
        // have been moved into the anime library despite being Western
        // co-productions. Require a Japanese origin before trusting "true".
        if (
            isAnime == true
            && originCountry is not null
            && !originCountry.Any(c => string.Equals(c, "JP", StringComparison.OrdinalIgnoreCase))
        )
            isAnime = false;

        return isAnime switch
        {
            true => "anime",
            false => "tv",
            null => null,
        };
    }
}
