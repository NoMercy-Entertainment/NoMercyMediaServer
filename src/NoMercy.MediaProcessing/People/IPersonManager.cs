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

namespace NoMercy.MediaProcessing.People;

public interface IPersonManager
{
    public Task Store(TmdbTvShowAppends show);
    public Task UpdatePersonAsync(int personId);
    public Task Update(string showName, TmdbTvShowAppends show);
    public Task Remove(string showName, TmdbTvShowAppends show);
}
