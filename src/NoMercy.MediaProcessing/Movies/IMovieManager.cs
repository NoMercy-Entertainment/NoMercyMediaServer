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

using NoMercy.Database.Models.Libraries;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Movies;

public interface IMovieManager
{
    Task<TmdbMovieAppends?> Add(int id, Library library);
    Task Update(int id, Library library);
    Task Remove(int id, Library library);
}
