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

using NoMercy.Providers.TVDB.Models.Lists;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbListClient : TvdbBaseClient
{
    public TvdbListClient(int id = 0, string language = "eng")
        : base(id: id, language: language) { }

    public Task<TvdbPaginatedResponse<TvdbList>?> All(int page = 0, bool? priority = false)
    {
        Dictionary<string, string?> query = new() { [key: "page"] = page.ToString() };
        return Get<TvdbPaginatedResponse<TvdbList>>(url: "lists", query: query, priority: priority);
    }

    public Task<TvdbListResponse?> Details(bool? priority = false)
    {
        return Get<TvdbListResponse>(url: "lists/" + Id, priority: priority);
    }

    public Task<TvdbListExtendedResponse?> Extended(bool? priority = false)
    {
        return Get<TvdbListExtendedResponse>(url: "lists/" + Id + "/extended", priority: priority);
    }

    public Task<TvdbListResponse?> BySlug(string slug, bool? priority = false)
    {
        return Get<TvdbListResponse>(url: "lists/slug/" + slug, priority: priority);
    }

    public Task<TvdbListTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbListTranslationResponse>(
            url: $"lists/{Id}/translations/{language}",
            priority: priority
        );
    }
}
