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

using NoMercy.Providers.TVDB.Models.Inspirations;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbInspirationClient : TvdbBaseClient
{
    public Task<TvdbInspirationTypesResponse?> InspirationTypes(bool? priority = false)
    {
        return Get<TvdbInspirationTypesResponse>("inspiration/types", priority: priority);
    }
}
