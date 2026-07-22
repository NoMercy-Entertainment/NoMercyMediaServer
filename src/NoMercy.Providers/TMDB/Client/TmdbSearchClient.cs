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
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbSearchClient : TmdbBaseClient
{
    public Task<TmdbPaginatedResponse<TmdbMovie>?> Movie(
        string query,
        string? year = "",
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "query"] = query,
            [key: "primary_release_year"] = year,
        };

        return Get<TmdbPaginatedResponse<TmdbMovie>>(url: "search/movie", query: queryParams, priority: priority);
    }

    public Task<TmdbPaginatedResponse<TmdbTvShow>?> TvShow(
        string query,
        string? year = "",
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "query"] = query,
            [key: "first_air_date_year"] = year,
        };

        return Get<TmdbPaginatedResponse<TmdbTvShow>>(url: "search/tv", query: queryParams, priority: priority);
    }

    public Task<TmdbPaginatedResponse<TmdbPerson>?> Person(
        string query,
        string? year = "",
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "query"] = query,
            [key: "primary_release_year"] = year,
        };

        return Get<TmdbPaginatedResponse<TmdbPerson>>(url: "search/person", query: queryParams, priority: priority);
    }

    public Task<TmdbPaginatedResponse<TmdbMultiSearch>?> Multi(
        string query,
        string? year = "",
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "query"] = query,
            [key: "primary_release_year"] = year,
        };

        return Get<TmdbPaginatedResponse<TmdbMultiSearch>>(url: "search/multi", query: queryParams, priority: priority);
    }

    public Task<TmdbPaginatedResponse<TmdbCollection>?> Collection(
        string query,
        string? year = "",
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "query"] = query,
            [key: "primary_release_year"] = year,
        };

        return Get<TmdbPaginatedResponse<TmdbCollection>>(
            url: "search/collection",
            query: queryParams,
            priority: priority
        );
    }

    public Task<TmdbPaginatedResponse<TmdbKeyword>?> Keyword(
        string query,
        string? year = "",
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "query"] = query,
            [key: "primary_release_year"] = year,
        };

        return Get<TmdbPaginatedResponse<TmdbKeyword>>(url: "search/keyword", query: queryParams, priority: priority);
    }
}
