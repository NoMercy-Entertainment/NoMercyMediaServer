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

using NoMercy.Providers.TMDB.Models.Genres;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using TmdbMovieCertifications = NoMercy.Providers.TMDB.Models.Certifications.TmdbMovieCertifications;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbMovieClient : TmdbBaseClient, ITmdbMovieClient
{
    private readonly Func<TmdbMovieAppends?>? _mockAppendsProvider;

    public TmdbMovieClient(
        int? id = 0,
        string[]? appendices = null,
        Func<TmdbMovieAppends?>? mockAppendsProvider = null,
        string? language = "en-US"
    )
        : base(id: (int)id!, language: language!)
    {
        _mockAppendsProvider = mockAppendsProvider;
    }

    public Task<TmdbMovieDetails?> Details(bool? priority = false)
    {
        return Get<TmdbMovieDetails>(url: "movie/" + Id, priority: priority);
    }

    private Task<TmdbMovieAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "append_to_response"] = string.Join(separator: ",", value: appendices),
        };

        return Get<TmdbMovieAppends>(url: "movie/" + Id, query: queryParams, priority: priority);
    }

    public Task<TmdbMovieAppends?> WithAllAppends(bool? priority = false)
    {
        if (_mockAppendsProvider != null)
        {
            return Task.FromResult(result: _mockAppendsProvider());
        }

        return WithAppends(
            appendices:
            [
                "alternative_titles",
                "release_dates",
                "changes",
                "credits",
                "keywords",
                "recommendations",
                "similar",
                "translations",
                "external_ids",
                "videos",
                "images",
                "watch/providers",
            ],
            priority: priority
        );
    }

    public Task<TmdbMovieAggregatedCredits?> AggregatedCredits(bool? priority = false)
    {
        return Get<TmdbMovieAggregatedCredits>(
            url: "movie/" + Id + "/aggregate_credits",
            priority: priority
        );
    }

    public Task<TmdbMovieAlternativeTitles?> AlternativeTitles(bool? priority = false)
    {
        return Get<TmdbMovieAlternativeTitles>(
            url: "movie/" + Id + "/alternative_titles",
            priority: priority
        );
    }

    public Task<TmdbMovieChanges?> Changes(string startDate, string endDate)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "start_date"] = startDate,
            [key: "end_date"] = endDate,
        };

        return Get<TmdbMovieChanges>(url: "movie/" + Id + "/changes", query: queryParams);
    }

    public Task<TmdbMovieCredits?> Credits(bool? priority = false)
    {
        return Get<TmdbMovieCredits>(url: "movie/" + Id + "/credits", priority: priority);
    }

    public Task<TmdbMovieExternalIds?> ExternalIds(bool? priority = false)
    {
        return Get<TmdbMovieExternalIds>(url: "movie/" + Id + "/external_ids", priority: priority);
    }

    public Task<TmdbImages?> Images(bool? priority = false)
    {
        return Get<TmdbImages>(url: "movie/" + Id + "/images", priority: priority);
    }

    public Task<TmdbMovieKeywords?> Keywords(bool? priority = false)
    {
        return Get<TmdbMovieKeywords>(url: "movie/" + Id + "/keywords", priority: priority);
    }

    public Task<TmdbMovieLists?> Lists(bool? priority = false)
    {
        return Get<TmdbMovieLists>(url: "movie/" + Id + "/lists", priority: priority);
    }

    public Task<TmdbMovieRecommendations?> Recommendations(bool? priority = false)
    {
        return Get<TmdbMovieRecommendations>(
            url: "movie/" + Id + "/recommendations",
            priority: priority
        );
    }

    public Task<TmdbMovieReleaseDates?> ReleaseDates(bool? priority = false)
    {
        return Get<TmdbMovieReleaseDates>(url: "movie/" + Id + "/release_dates", priority: priority);
    }

    public Task<TmdbMovieReviews?> Reviews(bool? priority = false)
    {
        return Get<TmdbMovieReviews>(url: "movie/" + Id + "/reviews", priority: priority);
    }

    public Task<TmdbMovieSimilar?> Similar(bool? priority = false)
    {
        return Get<TmdbMovieSimilar>(url: "movie/" + Id + "/similar", priority: priority);
    }

    public Task<TmdbSharedTranslations?> Translations(bool? priority = false)
    {
        return Get<TmdbSharedTranslations>(url: "movie/" + Id + "/translations", priority: priority);
    }

    public Task<TmdbMovieVideos?> Videos(bool? priority = false)
    {
        return Get<TmdbMovieVideos>(url: "movie/" + Id + "/videos", priority: priority);
    }

    public Task<TmdbWatchProviders?> WatchProviders(bool? priority = false)
    {
        return Get<TmdbWatchProviders>(url: "movie/" + Id + "/watch/providers", priority: priority);
    }

    public Task<TmdbMovieLatest?> Latest(bool? priority = false)
    {
        return Get<TmdbMovieLatest>(url: "movie/" + Id + "/latest", priority: priority);
    }

    public Task<TmdbMovieNowPlaying?> NowPlaying(bool? priority = false)
    {
        return Get<TmdbMovieNowPlaying>(url: "movie/" + Id + "/now_playing", priority: priority);
    }

    public Task<List<TmdbMovie>?> Popular(int limit = 10)
    {
        return Paginated<TmdbMovie>(url: "movie/popular", limit: limit);
    }

    public Task<TmdbMovieTopRated?> TopRated(bool? priority = false)
    {
        return Get<TmdbMovieTopRated>(url: "movie/" + Id + "/top_rated", priority: priority);
    }

    public Task<TmdbMovieUpcoming?> Upcoming(bool? priority = false)
    {
        return Get<TmdbMovieUpcoming>(url: "movie/" + Id + "/upcoming", priority: priority);
    }

    public Task<TmdbMovieCertifications?> Certifications(bool? priority = false)
    {
        return Get<TmdbMovieCertifications>(url: "certification/movie/list", priority: priority);
    }

    public Task<TmdbGenreMovies?> Genres(string language = "en", bool? priority = false)
    {
        return Get<TmdbGenreMovies>(
            url: "genre/movie/list",
            query: new Dictionary<string, string?> { [key: "language"] = language },
            priority: priority
        );
    }
}
