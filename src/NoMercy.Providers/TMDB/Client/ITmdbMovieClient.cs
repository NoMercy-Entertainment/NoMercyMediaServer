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

using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Networks;

namespace NoMercy.Providers.TMDB.Client;

public interface ITmdbMovieClient
{
    Task<TmdbMovieAppends?> WithAllAppends(bool? append);
    Task<TmdbTmdbNetworkDetails?> CompanyDetails(int id, bool? priority = false);
}
