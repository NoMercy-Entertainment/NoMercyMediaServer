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

// ReSharper disable All

using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzArtistClient : MusicBrainzBaseClient
{
    public MusicBrainzArtistClient()
        : base() { }

    public MusicBrainzArtistClient(Guid? id, string[]? appendices = null)
        : base((Guid)id!) { }

    public Task<MusicBrainzArtistAppends?> WithAppends(
        Guid? id,
        string[] appendices,
        bool? priority = false
    )
    {
        Dictionary<string, string> queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json",
        };

        return Get<MusicBrainzArtistAppends>("artist/" + id, queryParams, priority);
    }

    public Task<MusicBrainzArtistAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json",
        };

        return Get<MusicBrainzArtistAppends>("artist/" + Id, queryParams, priority);
    }

    public Task<MusicBrainzArtistAppends?> WithAllAppends(Guid? id, bool? priority = false)
    {
        return WithAppends(
            (Guid)id!,
            ["genres", "recordings", "releases", "release-groups", "works"],
            priority
        );
    }

    public Task<MusicBrainzArtistAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(
            ["genres", "recordings", "releases", "release-groups", "works"],
            priority
        );
    }

    public Task<MusicBrainzArtistAppends?> SearchArtists(string query, bool? priority = false)
    {
        Dictionary<string, string>? queryParams = new() { ["query"] = query, ["fmt"] = "json" };

        return Get<MusicBrainzArtistAppends>("artist", queryParams, priority);
    }
}
