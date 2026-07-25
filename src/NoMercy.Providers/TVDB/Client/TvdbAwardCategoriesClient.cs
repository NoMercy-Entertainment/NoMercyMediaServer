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

using NoMercy.Providers.TVDB.Models.Awards;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbAwardCategoriesClient : TvdbBaseClient
{
    public TvdbAwardCategoriesClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbAwardCategoryResponse?> Details(bool? priority = false)
    {
        return Get<TvdbAwardCategoryResponse>("awards/categories/" + Id, priority: priority);
    }

    public Task<TvdbAwardCategoryExtendedResponse?> Extended(bool? priority = false)
    {
        return Get<TvdbAwardCategoryExtendedResponse>(
            "awards/categories/" + Id + "/extended",
            priority: priority
        );
    }
}
