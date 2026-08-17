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
        return ClassifyAsync(show.Name, show.FirstAirDate.ParseYear());
    }

    public async Task<string?> ClassifyAsync(string name, int? year)
    {
        bool? isAnime = await KitsuIoClient.IsAnime(name, year ?? 0);
        return isAnime switch
        {
            true => "anime",
            false => "tv",
            null => null,
        };
    }
}
