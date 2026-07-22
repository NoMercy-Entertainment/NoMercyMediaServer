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

using NoMercy.Providers.TVDB.Models.Genres;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbGenresClient : TvdbBaseClient
{
    public TvdbGenresClient(int id = 0, string language = "eng")
        : base(id: id, language: language) { }

    public Task<TvdbGenresResponse?> Genres(bool? priority = false)
    {
        return Get<TvdbGenresResponse>(url: "genres", priority: priority);
    }

    public Task<TvdbGenreResponse?> Details(bool? priority = false)
    {
        return Get<TvdbGenreResponse>(url: "genres/" + Id, priority: priority);
    }
}
