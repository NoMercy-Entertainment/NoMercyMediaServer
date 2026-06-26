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

using NoMercy.Providers.TVDB.Models.Tags;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbTagsClient : TvdbBaseClient
{
    public TvdbTagsClient(int id = 0, string language = "eng")
        : base(id, language) { }

    public Task<TvdbTagOptionsResponse?> Options(bool? priority = false)
    {
        return Get<TvdbTagOptionsResponse>("tags/options", priority: priority);
    }

    public Task<TvdbTagOptionResponse?> Details(bool? priority = false)
    {
        return Get<TvdbTagOptionResponse>("tags/options/" + Id, priority: priority);
    }
}
