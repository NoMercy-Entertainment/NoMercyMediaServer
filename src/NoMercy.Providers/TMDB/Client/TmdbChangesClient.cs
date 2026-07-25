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

using Microsoft.AspNetCore.WebUtilities;
using NoMercy.Providers.TMDB.Models.Shared;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

/// <summary>
/// Global "changes" list endpoints. TMDB returns the ids of every movie, tv show or
/// person that changed in the requested window so a catalog can be kept in sync without
/// re-fetching the full append payload for every item.
/// https://developer.themoviedb.org/reference/changes-movie-list
/// </summary>
public class TmdbChangesClient : TmdbBaseClient
{
    public Task<List<TmdbChangeListItem>?> MovieChanges(
        string? startDate = null,
        string? endDate = null,
        int limit = 500
    )
    {
        return ChangeList("movie/changes", startDate, endDate, limit);
    }

    public Task<List<TmdbChangeListItem>?> TvChanges(
        string? startDate = null,
        string? endDate = null,
        int limit = 500
    )
    {
        return ChangeList("tv/changes", startDate, endDate, limit);
    }

    public Task<List<TmdbChangeListItem>?> PersonChanges(
        string? startDate = null,
        string? endDate = null,
        int limit = 500
    )
    {
        return ChangeList("person/changes", startDate, endDate, limit);
    }

    private Task<List<TmdbChangeListItem>?> ChangeList(
        string resource,
        string? startDate,
        string? endDate,
        int limit
    )
    {
        Dictionary<string, string?> queryParams = new();

        if (startDate is not null)
            queryParams["start_date"] = startDate;
        if (endDate is not null)
            queryParams["end_date"] = endDate;

        string url =
            queryParams.Count > 0 ? QueryHelpers.AddQueryString(resource, queryParams) : resource;

        return Paginated<TmdbChangeListItem>(url, limit);
    }
}
