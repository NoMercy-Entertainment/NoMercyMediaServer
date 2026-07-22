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

using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbTvShowMetadataProvider : ITvShowMetadataProvider
{
    public async Task<TmdbTvShowAppends?> GetTvShowAsync(
        int id,
        string language,
        CancellationToken ct = default
    )
    {
        using TmdbTvClient tmdbTvClient = new(id: id, language: language);
        return await tmdbTvClient.WithAllAppends(priority: true);
    }

    public async Task<TmdbTvShowDetails?> GetTvShowDetailsAsync(
        int id,
        CancellationToken ct = default
    )
    {
        using TmdbTvClient tmdbTvClient = new(id: id);
        return await tmdbTvClient.Details(priority: true);
    }
}
