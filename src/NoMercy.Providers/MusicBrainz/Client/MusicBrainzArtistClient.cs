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
        : base(id: (Guid)id!) { }

    public Task<MusicBrainzArtistAppends?> WithAppends(
        Guid? id,
        string[] appendices,
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "inc"] = string.Join(separator: "+", value: appendices),
            [key: "fmt"] = "json",
        };

        return Get<MusicBrainzArtistAppends>(url: "artist/" + id, query: queryParams, priority: priority);
    }

    public Task<MusicBrainzArtistAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "inc"] = string.Join(separator: "+", value: appendices),
            [key: "fmt"] = "json",
        };

        return Get<MusicBrainzArtistAppends>(url: "artist/" + Id, query: queryParams, priority: priority);
    }

    public Task<MusicBrainzArtistAppends?> WithAllAppends(Guid? id, bool? priority = false)
    {
        return WithAppends(
            id: (Guid)id!,
            appendices: ["genres", "recordings", "releases", "release-groups", "works"],
            priority: priority
        );
    }

    public Task<MusicBrainzArtistAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(
            appendices: ["genres", "recordings", "releases", "release-groups", "works"],
            priority: priority
        );
    }

    public Task<MusicBrainzArtistAppends?> SearchArtists(string query, bool? priority = false)
    {
        Dictionary<string, string?>? queryParams = new() { [key: "query"] = query, [key: "fmt"] = "json" };

        return Get<MusicBrainzArtistAppends>(url: "artist", query: queryParams, priority: priority);
    }
}
