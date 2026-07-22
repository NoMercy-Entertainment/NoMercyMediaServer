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

using NoMercy.Providers.TMDB.Models.Collections;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbCollectionClient : TmdbBaseClient
{
    public TmdbCollectionClient(int id, string[]? appendices = null, string? language = "en-US")
        : base(id: id, language: language!) { }

    public Task<TmdbCollectionDetails?> Details()
    {
        return Get<TmdbCollectionDetails>(url: "collection/" + Id);
    }

    private Task<TmdbCollectionAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "append_to_response"] = string.Join(separator: ",", value: appendices),
        };

        return Get<TmdbCollectionAppends>(url: "collection/" + Id, query: queryParams, priority: priority);
    }

    public Task<TmdbCollectionAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(appendices: ["images", "translations"], priority: priority);
    }

    public Task<TmdbCollectionImages?> Images()
    {
        return Get<TmdbCollectionImages>(url: "collection/" + Id + "/images");
    }

    public Task<TmdbCollectionsTranslations?> Translations()
    {
        return Get<TmdbCollectionsTranslations>(url: "collection/" + Id + "/translations");
    }
}
