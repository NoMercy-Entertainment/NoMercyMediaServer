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

using NoMercy.Providers.TVDB.Models.Movies;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbMovieClient : TvdbBaseClient
{
    public TvdbMovieClient(int id = 0, string language = "eng")
        : base(id: id, language: language) { }

    public Task<TvdbMovieResponse?> Details(bool? priority = false)
    {
        return Get<TvdbMovieResponse>(url: "movies/" + Id, priority: priority);
    }

    public Task<TvdbMovieExtendedResponse?> Extended(
        string? meta = null,
        bool shortMeta = false,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new();
        if (!string.IsNullOrEmpty(value: meta))
            query[key: "meta"] = meta;
        if (shortMeta)
            query[key: "short"] = "true";
        return Get<TvdbMovieExtendedResponse>(url: "movies/" + Id + "/extended", query: query, priority: priority);
    }

    public Task<TvdbMovieExtendedResponse?> WithAllAppends(bool? priority = false)
    {
        return Extended(meta: "translations", shortMeta: false, priority: priority);
    }

    public Task<TvdbMovieTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbMovieTranslationResponse>(
            url: $"movies/{Id}/translations/{language}",
            priority: priority
        );
    }

    public Task<TvdbResponse<TvdbMovie>?> BySlug(string slug, bool? priority = false)
    {
        return Get<TvdbResponse<TvdbMovie>>(url: "movies/slug/" + slug, priority: priority);
    }

    public Task<TvdbMovieStatusesResponse?> Statuses(bool? priority = false)
    {
        return Get<TvdbMovieStatusesResponse>(url: "movies/statuses", priority: priority);
    }

    public Task<TvdbPaginatedResponse<TvdbMovie>?> All(int page = 0, bool? priority = false)
    {
        Dictionary<string, string?> query = new() { [key: "page"] = page.ToString() };
        return Get<TvdbPaginatedResponse<TvdbMovie>>(url: "movies", query: query, priority: priority);
    }

    public Task<TvdbPaginatedResponse<TvdbMovie>?> Filter(
        TvdbMovieFilter filter,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = filter.ToQuery();
        return Get<TvdbPaginatedResponse<TvdbMovie>>(url: "movies/filter", query: query, priority: priority);
    }
}

public class TvdbMovieFilter
{
    public string? Country { get; set; }
    public string? Language { get; set; }
    public int? CompanyId { get; set; }
    public int? ContentRatingId { get; set; }
    public string? GenreIds { get; set; }
    public int? SortBy { get; set; }
    public string? SortType { get; set; }
    public int? Status { get; set; }
    public int? Year { get; set; }
    public int Page { get; set; }

    internal Dictionary<string, string?> ToQuery()
    {
        Dictionary<string, string?> q = new();
        if (Country is not null)
            q[key: "country"] = Country;
        if (Language is not null)
            q[key: "lang"] = Language;
        if (CompanyId is not null)
            q[key: "company"] = CompanyId.Value.ToString();
        if (ContentRatingId is not null)
            q[key: "contentRating"] = ContentRatingId.Value.ToString();
        if (!string.IsNullOrEmpty(value: GenreIds))
            q[key: "genre"] = GenreIds;
        if (SortBy is not null)
            q[key: "sort"] = SortBy.Value.ToString();
        if (!string.IsNullOrEmpty(value: SortType))
            q[key: "sortType"] = SortType;
        if (Status is not null)
            q[key: "status"] = Status.Value.ToString();
        if (Year is not null)
            q[key: "year"] = Year.Value.ToString();
        q[key: "page"] = Page.ToString();
        return q;
    }
}
