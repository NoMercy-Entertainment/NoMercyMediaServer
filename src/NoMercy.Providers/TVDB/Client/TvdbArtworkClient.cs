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

namespace NoMercy.Providers.TVDB.Client;

public class TvdbArtworkClient : TvdbBaseClient
{
    public TvdbArtworkClient(int id = 0, string language = "eng")
        : base(id: id, language: language) { }

    public Task<TvdbArtworkResponse?> Details(bool? priority = false)
    {
        return Get<TvdbArtworkResponse>(url: "artwork/" + Id, priority: priority);
    }

    public Task<TvdbArtworkExtendedResponse?> Extended(bool? priority = false)
    {
        return Get<TvdbArtworkExtendedResponse>(url: "artwork/" + Id + "/extended", priority: priority);
    }

    public Task<TvdbArtworkStatusesResponse?> Statuses(bool? priority = false)
    {
        return Get<TvdbArtworkStatusesResponse>(url: "artwork/statuses", priority: priority);
    }

    public Task<TvdbArtworkTypesResponse?> Types(bool? priority = false)
    {
        return Get<TvdbArtworkTypesResponse>(url: "artwork/types", priority: priority);
    }
}
