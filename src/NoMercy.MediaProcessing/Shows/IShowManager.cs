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
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

public interface IShowManager
{
    Task<TmdbTvShowAppends?> AddShowAsync(int id, Library library, bool? priority = false);
    Task UpdateShowAsync(int id, Library library);
    Task RemoveShowAsync(int id);
}
