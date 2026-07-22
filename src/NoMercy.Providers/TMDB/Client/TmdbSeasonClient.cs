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

using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbSeasonClient : TmdbBaseClient, IDisposable
{
    private readonly int _seasonNumber;

    public TmdbSeasonClient(
        int tvId,
        int seasonNumber,
        string[]? appendices = null,
        string? language = "en-US"
    )
        : base(id: tvId, language: language!)
    {
        _seasonNumber = seasonNumber;
    }

    public TmdbEpisodeClient Episode(int episodeNumber, string[]? items = null)
    {
        return new TmdbEpisodeClient(id: Id, seasonNumber: _seasonNumber, episodeNumber: episodeNumber);
    }

    public Task<TmdbSeasonDetails?> Details(bool? priority = false)
    {
        return Get<TmdbSeasonDetails>(url: "tv/" + Id + "/season/" + _seasonNumber, priority: priority);
    }

    public Task<TmdbSeasonAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "append_to_response"] = string.Join(separator: ",", value: appendices),
        };

        return Get<TmdbSeasonAppends>(
            url: "tv/" + Id + "/season/" + _seasonNumber,
            query: queryParams,
            priority: priority
        );
    }

    public Task<TmdbSeasonAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(
            appendices: ["aggregate_credits", "changes", "credits", "external_ids", "images", "translations"],
            priority: priority
        );
    }

    //public Task<AccountStates?> AccountStates()
    //{
    //    strreturn Get<Details>(("tv/" + Id + "/season/" + SeasonNumber + "/account_states");
    //
    //}

    public Task<TmdbSeasonAggregatedCredits?> AggregatedCredits(bool? priority = false)
    {
        return Get<TmdbSeasonAggregatedCredits>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/aggregate_credits",
            priority: priority
        );
    }

    public async Task<TmdbSeasonChanges?> Changes(
        string startDate,
        string endDate,
        bool? priority = false
    )
    {
        // First get the season details to obtain the season ID
        TmdbSeasonDetails? seasonDetails = await Details(priority: priority);
        if (seasonDetails == null)
            return null;

        Dictionary<string, string?> queryParams = new()
        {
            [key: "start_date"] = startDate,
            [key: "end_date"] = endDate,
        };

        // Use the season ID for the changes endpoint
        return await Get<TmdbSeasonChanges>(
            url: "tv/season/" + seasonDetails.Id + "/changes",
            query: queryParams,
            priority: priority
        );
    }

    public Task<TmdbSeasonCredits?> Credits(bool? priority = false)
    {
        return Get<TmdbSeasonCredits>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/credits",
            priority: priority
        );
    }

    public Task<TmdbSeasonExternalIds?> ExternalIds(bool? priority = false)
    {
        return Get<TmdbSeasonExternalIds>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/external_ids",
            priority: priority
        );
    }

    public Task<TmdbSeasonImages?> Images(bool? priority = false)
    {
        return Get<TmdbSeasonImages>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/images",
            priority: priority
        );
    }

    public Task<TmdbSharedTranslations?> Translations(bool? priority = false)
    {
        return Get<TmdbSharedTranslations>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/translations",
            priority: priority
        );
    }

    public Task<TmdbSeasonVideos?> Videos(bool? priority = false)
    {
        return Get<TmdbSeasonVideos>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/videos",
            priority: priority
        );
    }

    public new void Dispose()
    {
        base.Dispose();
    }
}
