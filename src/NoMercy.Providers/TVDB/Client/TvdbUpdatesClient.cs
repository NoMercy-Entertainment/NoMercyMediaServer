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

using NoMercy.Providers.TVDB.Models.Updates;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbUpdatesClient : TvdbBaseClient
{
    public Task<TvdbUpdatesResponse?> Updates(
        long since,
        string? type = null,
        string? action = null,
        int page = 0,
        bool? priority = false
    )
    {
        Dictionary<string, string?> query = new()
        {
            [key: "since"] = since.ToString(),
            [key: "page"] = page.ToString(),
        };
        if (!string.IsNullOrEmpty(value: type))
            query[key: "type"] = type;
        if (!string.IsNullOrEmpty(value: action))
            query[key: "action"] = action;
        return Get<TvdbUpdatesResponse>(url: "updates", query: query, priority: priority);
    }
}
