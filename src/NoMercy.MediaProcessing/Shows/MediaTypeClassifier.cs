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
    public async Task<string> ClassifyAsync(TmdbTvShowAppends show)
    {
        bool isAnime = await KitsuIoClient.IsAnime(show.Name, show.FirstAirDate.ParseYear());

        // Kitsu alone isn't enough — require Japanese origin country from TMDB to
        // avoid false positives on western shows that have Kitsu entries
        // (e.g. co-productions).
        if (isAnime)
        {
            bool hasJapaneseOrigin = show.OriginCountry.Any(c =>
                string.Equals(c, "JP", StringComparison.OrdinalIgnoreCase)
            );
            if (!hasJapaneseOrigin)
                isAnime = false;
        }

        return isAnime ? "anime" : "tv";
    }
}
