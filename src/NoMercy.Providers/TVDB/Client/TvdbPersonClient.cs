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

using NoMercy.Providers.TVDB.Models.Characters;
using NoMercy.Providers.TVDB.Models.People;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbPersonClient : TvdbBaseClient
{
    public TvdbPersonClient(int id = 0, string language = "eng")
        : base(id: id, language: language) { }

    public Task<TvdbPersonResponse?> Details(bool? priority = false)
    {
        return Get<TvdbPersonResponse>(url: "people/" + Id, priority: priority);
    }

    public Task<TvdbPersonExtendedResponse?> Extended(string? meta = null, bool? priority = false)
    {
        Dictionary<string, string?> query = new();
        if (!string.IsNullOrEmpty(value: meta))
            query[key: "meta"] = meta;
        return Get<TvdbPersonExtendedResponse>(url: "people/" + Id + "/extended", query: query, priority: priority);
    }

    public Task<TvdbPersonExtendedResponse?> WithAllAppends(bool? priority = false)
    {
        return Extended(meta: "translations", priority: priority);
    }

    public Task<TvdbPersonTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbPersonTranslationResponse>(
            url: $"people/{Id}/translations/{language}",
            priority: priority
        );
    }

    public Task<TvdbPersonTypesResponse?> Types(bool? priority = false)
    {
        return Get<TvdbPersonTypesResponse>(url: "people/types", priority: priority);
    }

    public Task<TvdbCharacterResponse?> Character(bool? priority = false)
    {
        return Get<TvdbCharacterResponse>(url: "characters/" + Id, priority: priority);
    }
}
