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
        : base((Guid)id!) { }

    public Task<MusicBrainzReleaseAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json",
        };

        return Get<MusicBrainzReleaseAppends>("release-group/" + Id, queryParams, priority);
    }

    public Task<MusicBrainzReleaseAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(["artists", "releases"], priority);
    }

    public Task<MusicBrainzReleaseAppends?> SearchReleaseGroups(
        string query,
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new() { ["query"] = query, ["fmt"] = "json" };
        return Get<MusicBrainzReleaseAppends>("release-group", queryParams, priority);
    }
}
