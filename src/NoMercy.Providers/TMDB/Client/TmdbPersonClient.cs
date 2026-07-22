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

using NoMercy.Providers.TMDB.Models.People;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbPersonClient : TmdbBaseClient
{
    public TmdbPersonClient(int? id = 0, string[]? appendices = null)
        : base(id: (int)id!) { }

    public Task<TmdbPersonDetails?> Details()
    {
        return Get<TmdbPersonDetails>(url: "person/" + Id);
    }

    public Task<TmdbPersonAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "append_to_response"] = string.Join(separator: ",", value: appendices),
        };

        return Get<TmdbPersonAppends>(url: "person/" + Id, query: queryParams, priority: priority);
    }

    public Task<TmdbPersonAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(
            appendices:
            [
                "changes",
                "credits",
                "movie_credits",
                "combined_credits",
                "tv_credits",
                "external_ids",
                "images",
                "translations",
            ],
            priority: priority
        );
    }

    public Task<TmdbPersonChanges?> Changes(string startDate, string endDate)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "start_date"] = startDate,
            [key: "end_date"] = endDate,
        };

        return Get<TmdbPersonChanges>(url: "person/" + Id + "/changes", query: queryParams);
    }

    public Task<TmdbPersonCredits?> MovieCredits()
    {
        return Get<TmdbPersonCredits>(url: "person/" + Id + "/movie_credits");
    }

    public Task<TmdbPersonCredits?> TvCredits()
    {
        return Get<TmdbPersonCredits>(url: "person/" + Id + "/tv_credits");
    }

    public Task<TmdbPersonExternalIds?> ExternalIds()
    {
        return Get<TmdbPersonExternalIds>(url: "person/" + Id + "/external_ids");
    }

    public Task<TmdbPersonImages?> Images()
    {
        return Get<TmdbPersonImages>(url: "person/" + Id + "/images");
    }

    public Task<TmdbPersonTranslations?> Translations()
    {
        return Get<TmdbPersonTranslations>(url: "person/" + Id + "/translations");
    }

    public Task<List<TmdbPerson>?> Popular(int limit = 10)
    {
        return Paginated<TmdbPerson>(url: "person/popular", limit: limit);
    }
}
