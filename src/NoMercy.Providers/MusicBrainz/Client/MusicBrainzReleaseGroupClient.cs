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

// ReSharper disable MemberCanBePrivate.Global

using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzReleaseGroupClient : MusicBrainzBaseClient
{
    public MusicBrainzReleaseGroupClient(Guid? id)
        : base(id: (Guid)id!) { }

    public Task<MusicBrainzReleaseGroupDetails?> WithAppends(
        string[] appendices,
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "inc"] = string.Join(separator: "+", value: appendices),
            [key: "fmt"] = "json",
        };

        return Get<MusicBrainzReleaseGroupDetails>(url: "release-group/" + Id, query: queryParams, priority: priority);
    }

    public Task<MusicBrainzReleaseGroupDetails?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(appendices: ["artists", "releases"], priority: priority);
    }

    public Task<MusicBrainzReleaseGroupSearchResponse?> SearchReleaseGroups(
        string query,
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new() { [key: "query"] = query, [key: "fmt"] = "json" };
        return Get<MusicBrainzReleaseGroupSearchResponse>(url: "release-group", query: queryParams, priority: priority);
    }
}
