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

using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Shared;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbEpisodeClient : TmdbBaseClient
{
    private readonly int _episodeNumber;
    private readonly int _seasonNumber;

    public TmdbEpisodeClient(
        int id,
        int seasonNumber,
        int episodeNumber,
        string[]? appendices = null,
        string? language = "en-US"
    )
        : base(id: id, language: language!)
    {
        _seasonNumber = seasonNumber;
        _episodeNumber = episodeNumber;
    }

    public Task<TmdbEpisodeDetails?> Details(bool? priority = false)
    {
        return Get<TmdbEpisodeDetails>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber,
            priority: priority
        );
    }

    public Task<TmdbEpisodeAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "append_to_response"] = string.Join(separator: ",", value: appendices),
        };

        return Get<TmdbEpisodeAppends>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber,
            query: queryParams,
            priority: priority
        );
    }

    public Task<TmdbEpisodeAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(
            appendices: ["changes", "credits", "external_ids", "images", "translations", "videos"],
            priority: priority
        );
    }

    public Task<TmdbEpisodeChanges?> Changes(
        string startDate,
        string endDate,
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "start_date"] = startDate,
            [key: "end_date"] = endDate,
        };

        return Get<TmdbEpisodeChanges>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/changes",
            query: queryParams,
            priority: priority
        );
    }

    public Task<TmdbEpisodeCredits?> Credits(bool? priority = false)
    {
        return Get<TmdbEpisodeCredits>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/credits",
            priority: priority
        );
    }

    public Task<TmdbEpisodeExternalIds?> ExternalIds(bool? priority = false)
    {
        return Get<TmdbEpisodeExternalIds>(
            url: "tv/"
                 + Id
                 + "/season/"
                 + _seasonNumber
                 + "/episode/"
                 + _episodeNumber
                 + "/external_ids",
            priority: priority
        );
    }

    public Task<TmdbEpisodeImages?> Images(bool? priority = false)
    {
        return Get<TmdbEpisodeImages>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/images",
            priority: priority
        );
    }

    public Task<TmdbSharedTranslations?> Translations(bool? priority = false)
    {
        return Get<TmdbSharedTranslations>(
            url: "tv/"
                 + Id
                 + "/season/"
                 + _seasonNumber
                 + "/episode/"
                 + _episodeNumber
                 + "/translations",
            priority: priority
        );
    }

    public Task<Videos?> Videos(bool? priority = false)
    {
        return Get<Videos>(
            url: "tv/" + Id + "/season/" + _seasonNumber + "/episode/" + _episodeNumber + "/videos",
            priority: priority
        );
    }
}
