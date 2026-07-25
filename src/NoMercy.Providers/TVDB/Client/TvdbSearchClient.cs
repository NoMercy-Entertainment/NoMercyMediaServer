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

using NoMercy.Providers.TVDB.Models.Search;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbSearchClient : TvdbBaseClient
{
    public TvdbSearchClient(string language = "eng")
        : base(0, language) { }

    public Task<TvdbSearchResponse?> Search(
        string query,
        string? type = null,
        int? year = null,
        string? language = null,
        string? country = null,
        int? company = null,
        int? primaryType = null,
        string? network = null,
        int? remoteId = null,
        int offset = 0,
        int limit = 50,
        bool? priority = false
    )
    {
        Dictionary<string, string?> q = new() { ["query"] = query };
        if (!string.IsNullOrEmpty(type))
            q["type"] = type;
        if (year is not null)
            q["year"] = year.Value.ToString();
        if (!string.IsNullOrEmpty(language))
            q["language"] = language;
        if (!string.IsNullOrEmpty(country))
            q["country"] = country;
        if (company is not null)
            q["company"] = company.Value.ToString();
        if (primaryType is not null)
            q["primaryType"] = primaryType.Value.ToString();
        if (!string.IsNullOrEmpty(network))
            q["network"] = network;
        if (remoteId is not null)
            q["remote_id"] = remoteId.Value.ToString();
        q["offset"] = offset.ToString();
        q["limit"] = limit.ToString();
        return Get<TvdbSearchResponse>("search", q, priority);
    }

    public Task<TvdbSearchResponse?> Series(
        string query,
        int? year = null,
        string? language = null,
        bool? priority = false
    )
    {
        return Search(query, type: "series", year: year, language: language, priority: priority);
    }

    public Task<TvdbSearchResponse?> Movie(
        string query,
        int? year = null,
        string? language = null,
        bool? priority = false
    )
    {
        return Search(query, type: "movie", year: year, language: language, priority: priority);
    }

    public Task<TvdbSearchResponse?> Person(
        string query,
        string? language = null,
        bool? priority = false
    )
    {
        return Search(query, type: "person", language: language, priority: priority);
    }

    public Task<TvdbSearchResponse?> Company(
        string query,
        string? language = null,
        bool? priority = false
    )
    {
        return Search(query, type: "company", language: language, priority: priority);
    }

    public Task<TvdbSearchResponse?> ByRemoteId(string remoteId, bool? priority = false)
    {
        return Get<TvdbSearchResponse>("search/remoteid/" + remoteId, priority: priority);
    }
}
