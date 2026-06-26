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

using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Seasons;

public interface ISeasonManager
{
    Task<IEnumerable<TmdbSeasonAppends>> StoreSeasonsAsync(
        TmdbTvShowAppends show,
        bool? priority = false
    );
    Task UpdateSeasonAsync(string showName, TmdbSeasonAppends season);
    Task RemoveSeasonAsync(string showName, TmdbSeasonAppends season);
}
