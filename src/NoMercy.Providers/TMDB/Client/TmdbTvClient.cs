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

using NoMercy.Providers.TMDB.Models.Certifications;
using NoMercy.Providers.TMDB.Models.Genres;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbTvClient : TmdbBaseClient
{
    public TmdbTvClient(int? id = 0, string[]? appendices = null, string? language = "en-US")
        : base(id: (int)id!, language: language!) { }

    public TmdbSeasonClient Season(int seasonNumber, string[]? items = null)
    {
        return new TmdbSeasonClient(tvId: Id, seasonNumber: seasonNumber, appendices: items);
    }

    //public Task<Models?.Season.SeasonAppends> SeasonWithAppends(int SeasonNumber, string[] Appendices)
    //{
    //	return (new SeasonClient(Id, SeasonNumber)).WithAppends(Appendices);
    //}

    public Task<TmdbTvShowDetails?> Details(bool? priority = false)
    {
        return Get<TmdbTvShowDetails>(url: "tv/" + Id, priority: priority);
    }

    public Task<TmdbTvShowAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "append_to_response"] = string.Join(separator: ",", value: appendices),
        };

        return Get<TmdbTvShowAppends>(url: "tv/" + Id, query: queryParams, priority: priority);
    }

    public Task<TmdbTvShowAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(
            appendices:
            [
                "aggregate_credits",
                "alternative_titles",
                "changes",
                "content_ratings",
                "credits",
                "external_ids",
                "images",
                "keywords",
                "recommendations",
                "similar",
                "translations",
                "videos",
                "watch/providers",
            ],
            priority: priority
        );
    }

    public Task<TmdbTvAggregatedCredits?> AggregatedCredits(bool? priority = false)
    {
        return Get<TmdbTvAggregatedCredits>(url: "tv/" + Id + "/aggregate_credits", priority: priority);
    }

    public Task<TmdbTvAlternativeTitles?> AlternativeTitles(bool? priority = false)
    {
        return Get<TmdbTvAlternativeTitles>(url: "tv/" + Id + "/alternative_titles", priority: priority);
    }

    public Task<TmdbTvChanges?> Changes(string startDate, string endDate, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "start_date"] = startDate,
            [key: "end_date"] = endDate,
        };

        return Get<TmdbTvChanges>(url: "tv/" + Id + "/changes", query: queryParams, priority: priority);
    }

    public Task<TmdbTvContentRatings?> ContentRatings(bool? priority = false)
    {
        return Get<TmdbTvContentRatings>(url: "tv/" + Id + "/content_ratings", priority: priority);
    }

    public Task<TmdbTvCredits?> Credits(bool? priority = false)
    {
        return Get<TmdbTvCredits>(url: "tv/" + Id + "/credits", priority: priority);
    }

    public Task<TmdbTvEpisodeGroups?> EpisodeGroups(bool? priority = false)
    {
        return Get<TmdbTvEpisodeGroups>(url: "tv/" + Id + "/episode_groups", priority: priority);
    }

    public TmdbEpisodeGroupClient EpisodeGroup(string groupId)
    {
        return new TmdbEpisodeGroupClient(groupId: groupId);
    }

    public Task<TmdbTvExternalIds?> ExternalIds(bool? priority = false)
    {
        return Get<TmdbTvExternalIds>(url: "tv/" + Id + "/external_ids", priority: priority);
    }

    public Task<TmdbImages?> Images(bool? priority = false)
    {
        return Get<TmdbImages>(url: "tv/" + Id + "/images", priority: priority);
    }

    public Task<TmdbTvKeywords?> Keywords(bool? priority = false)
    {
        return Get<TmdbTvKeywords>(url: "tv/" + Id + "/keywords", priority: priority);
    }

    public Task<TmdbTvRecommendations?> Recommendations(bool? priority = false)
    {
        return Get<TmdbTvRecommendations>(url: "tv/" + Id + "/recommendations", priority: priority);
    }

    public Task<TmdbTvReviews?> Reviews(bool? priority = false)
    {
        return Get<TmdbTvReviews>(url: "tv/" + Id + "/reviews", priority: priority);
    }

    public Task<TmdbTvScreenedTheatrically?> ScreenedTheatrically(bool? priority = false)
    {
        return Get<TmdbTvScreenedTheatrically>(
            url: "tv/" + Id + "/screened_theatrically",
            priority: priority
        );
    }

    public Task<TmdbTvSimilar?> Similar(bool? priority = false)
    {
        return Get<TmdbTvSimilar>(url: "tv/" + Id + "/similar", priority: priority);
    }

    public Task<TmdbSharedTranslations?> Translations(bool? priority = false)
    {
        return Get<TmdbSharedTranslations>(url: "tv/" + Id + "/translations", priority: priority);
    }

    public Task<TmdbTvVideos?> Videos(bool? priority = false)
    {
        return Get<TmdbTvVideos>(url: "tv/" + Id + "/videos", priority: priority);
    }

    public Task<TmdbWatchProviders?> WatchProviders(bool? priority = false)
    {
        return Get<TmdbWatchProviders>(url: "tv/" + Id + "/watch/providers", priority: priority);
    }

    public Task<TmdbTvShowLatest?> Latest(bool? priority = false)
    {
        return Get<TmdbTvShowLatest>(url: "tv/latest", priority: priority);
    }

    public Task<TmdbTvAiringToday?> AiringToday(bool? priority = false)
    {
        return Get<TmdbTvAiringToday>(url: "tv/airing_today", priority: priority);
    }

    public Task<TmdbTvOnTheAir?> OnTheAir(bool? priority = false)
    {
        return Get<TmdbTvOnTheAir>(url: "tv/on_the_air", priority: priority);
    }

    public async Task<List<TmdbTvShow>?> Popular(int limit = 10, bool? priority = false)
    {
        TmdbPaginatedResponse<TmdbTvShow>? response = await Get<TmdbPaginatedResponse<TmdbTvShow>>(
            url: "tv/popular",
            priority: priority
        );
        return response?.Results?.Take(count: limit).ToList();
    }

    public Task<TmdbTvTopRated?> TopRated(bool? priority = false)
    {
        return Get<TmdbTvTopRated>(url: "tv/top_rated", priority: priority);
    }

    public Task<TvShowCertifications?> Certifications(bool? priority = false)
    {
        return Get<TvShowCertifications>(url: "certification/tv/list", priority: priority);
    }

    public Task<TmdbGenreTv?> Genres(string language = "en", bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new() { [key: "language"] = language };

        return Get<TmdbGenreTv>(url: "genre/tv/list", query: queryParams, priority: priority);
    }

    public Task<TmdbTmdbNetworkDetails?> NetworkDetails(int id, bool? priority = false)
    {
        return Get<TmdbTmdbNetworkDetails>(url: "network/" + id, priority: priority);
    }

}
