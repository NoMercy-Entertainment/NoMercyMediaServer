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

using NoMercy.Providers.TVDB.Models.ContentRatings;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbContentRatingClient : TvdbBaseClient
{
    public Task<TvdbContentRatingsResponse?> ContentRatings(bool? priority = false)
    {
        return Get<TvdbContentRatingsResponse>("content/ratings", priority: priority);
    }
}
