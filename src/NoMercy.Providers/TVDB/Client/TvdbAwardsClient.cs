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

public class TvdbAwardsClient : TvdbBaseClient
{
    public TvdbAwardsClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbAwardsResponse?> Awards(bool? priority = false)
    {
        return Get<TvdbAwardsResponse>("awards", priority: priority);
    }

    public Task<TvdbAwardResponse?> Details(bool? priority = false)
    {
        return Get<TvdbAwardResponse>("awards/" + Id, priority: priority);
    }

    public Task<TvdbAwardExtendedResponse?> Extended(bool? priority = false)
    {
        return Get<TvdbAwardExtendedResponse>("awards/" + Id + "/extended", priority: priority);
    }
}
