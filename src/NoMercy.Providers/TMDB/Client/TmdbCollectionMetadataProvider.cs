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

using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbCollectionMetadataProvider : ICollectionMetadataProvider
{
    public async Task<TmdbCollectionAppends?> GetCollectionAsync(
        int id,
        string language,
        CancellationToken ct = default
    )
    {
        using TmdbCollectionClient tmdbCollectionClient = new(id, language: language);
        return await tmdbCollectionClient.WithAllAppends(true);
    }
}
