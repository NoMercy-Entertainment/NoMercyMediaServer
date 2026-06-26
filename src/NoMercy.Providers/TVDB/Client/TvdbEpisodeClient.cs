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

using NoMercy.Providers.TVDB.Models.Episodes;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbEpisodeClient : TvdbBaseClient
{
    public TvdbEpisodeClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbEpisodeResponse?> Details(bool? priority = false)
    {
        return Get<TvdbEpisodeResponse>("episodes/" + Id, priority: priority);
    }

    public Task<TvdbEpisodeExtendedResponse?> Extended(string? meta = null, bool? priority = false)
    {
        Dictionary<string, string?> query = new();
        if (!string.IsNullOrEmpty(meta))
            query["meta"] = meta;
        return Get<TvdbEpisodeExtendedResponse>("episodes/" + Id + "/extended", query, priority);
    }

    public Task<TvdbEpisodeExtendedResponse?> WithAllAppends(bool? priority = false)
    {
        return Extended("translations", priority);
    }

    public Task<TvdbEpisodeTranslationResponse?> Translation(
        string language,
        bool? priority = false
    )
    {
        return Get<TvdbEpisodeTranslationResponse>(
            $"episodes/{Id}/translations/{language}",
            priority: priority
        );
    }
}
