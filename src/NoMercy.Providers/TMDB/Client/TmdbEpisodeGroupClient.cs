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

namespace NoMercy.Providers.TMDB.Client;

public class TmdbEpisodeGroupClient : TmdbBaseClient
{
    private readonly string _groupId;

    public TmdbEpisodeGroupClient(string groupId)
    {
        _groupId = groupId;
    }

    public Task<TmdbEpisodeGroupDetails?> Details(bool? priority = false)
    {
        return Get<TmdbEpisodeGroupDetails>("tv/episode_group/" + _groupId, priority: priority);
    }
}
