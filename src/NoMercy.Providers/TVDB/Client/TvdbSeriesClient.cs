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

using NoMercy.Providers.TVDB.Models.Artwork;
using NoMercy.Providers.TVDB.Models.Series;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbSeriesClient : TvdbBaseClient
{
    public TvdbSeriesClient(int id = 0, string language = "eng")
        : base(id: id, language: language) { }

    public Task<TvdbSeriesResponse?> Details(bool? priority = false)
    {
        return Get<TvdbSeriesResponse>(url: "series/" + Id, priority: priority);
    }

    public Task<TvdbSeriesExtendedResponse?> Extended(
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
        return Get<TvdbSeriesExtendedResponse>(url: "series/" + Id + "/extended", query: query, priority: priority);
    }

    public Task<TvdbSeriesExtendedResponse?> WithAllAppends(bool? priority = false)
    {
        return Extended(meta: "translations,episodes", shortMeta: false, priority: priority);
    }

    public Task<TvdbSeriesEpisodesResponse?> Episodes(
        string seasonType = "default",
        int page = 0,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new() { [key: "page"] = page.ToString() };
        return Get<TvdbSeriesEpisodesResponse>(
            url: $"series/{Id}/episodes/{seasonType}",
            query: query,
            priority: priority
        );
    }

    public Task<TvdbSeriesEpisodesResponse?> EpisodesTranslated(
        string seasonType,
        string language,
        int page = 0,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new() { [key: "page"] = page.ToString() };
        return Get<TvdbSeriesEpisodesResponse>(
            url: $"series/{Id}/episodes/{seasonType}/{language}",
            query: query,
            priority: priority
        );
    }

    public Task<TvdbSeriesTranslationResponse?> Translation(string language, bool? priority = false)
    {
        return Get<TvdbSeriesTranslationResponse>(
            url: $"series/{Id}/translations/{language}",
            priority: priority
        );
    }

    public Task<TvdbResponse<TvdbArtwork[]>?> Artworks(
        int? type = null,
        string? language = null,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new();
        if (type is not null)
            query[key: "type"] = type.Value.ToString();
        if (!string.IsNullOrEmpty(value: language))
            query[key: "lang"] = language;
        return Get<TvdbResponse<TvdbArtwork[]>>(url: "series/" + Id + "/artworks", query: query, priority: priority);
    }

    public Task<TvdbNextAiredResponse?> NextAired(bool? priority = false)
    {
        return Get<TvdbNextAiredResponse>(url: "series/" + Id + "/nextAired", priority: priority);
    }

    public Task<TvdbResponse<TvdbSeries>?> BySlug(string slug, bool? priority = false)
    {
        return Get<TvdbResponse<TvdbSeries>>(url: "series/slug/" + slug, priority: priority);
    }

    public Task<TvdbSeriesStatusesResponse?> Statuses(bool? priority = false)
    {
        return Get<TvdbSeriesStatusesResponse>(url: "series/statuses", priority: priority);
    }

    public Task<TvdbPaginatedResponse<TvdbSeries>?> All(int page = 0, bool? priority = false)
    {
        Dictionary<string, string?> query = new() { [key: "page"] = page.ToString() };
        return Get<TvdbPaginatedResponse<TvdbSeries>>(url: "series", query: query, priority: priority);
    }

    public Task<TvdbPaginatedResponse<TvdbSeries>?> Filter(
        TvdbSeriesFilter filter,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = filter.ToQuery();
        return Get<TvdbPaginatedResponse<TvdbSeries>>(url: "series/filter", query: query, priority: priority);
    }
}

public class TvdbSeriesFilter
{
    public string? Country { get; set; }
    public string? Language { get; set; }
    public int? CompanyId { get; set; }
    public int? ContentRatingId { get; set; }
    public string? GenreIds { get; set; }
    public int? Lang { get; set; }
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
