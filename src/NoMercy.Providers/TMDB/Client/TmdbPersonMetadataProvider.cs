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

using NoMercy.Providers.TMDB.Models.People;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbPersonMetadataProvider : IPersonMetadataProvider
{
    public async Task<TmdbPersonAppends?> GetPersonAsync(int id, CancellationToken ct = default)
    {
        using TmdbPersonClient tmdbPersonClient = new(id: id);
        return await tmdbPersonClient.WithAllAppends(priority: true);
    }
}
