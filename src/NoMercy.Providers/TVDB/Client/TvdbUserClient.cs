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

using NoMercy.Providers.TVDB.Models.User;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbUserClient : TvdbBaseClient
{
    public TvdbUserClient(int id = 0, string language = "eng")
        : base(id: id, language: language) { }

    public Task<TvdbUserResponse?> Me(bool? priority = false)
    {
        return Get<TvdbUserResponse>(url: "user", skipCache: true, priority: priority);
    }

    public Task<TvdbUserResponse?> Details(bool? priority = false)
    {
        return Get<TvdbUserResponse>(url: "user/" + Id, priority: priority);
    }

    public Task<TvdbUserFavoritesResponse?> Favorites(bool? priority = false)
    {
        return Get<TvdbUserFavoritesResponse>(
            url: "user/" + Id + "/favorites",
            skipCache: true,
            priority: priority
        );
    }
}
