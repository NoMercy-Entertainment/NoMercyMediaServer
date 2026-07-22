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

using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzReleaseClient : MusicBrainzBaseClient
{
    public MusicBrainzReleaseClient()
        : base() { }

    public MusicBrainzReleaseClient(Guid? id, string[]? appendices = null)
        : base(id: (Guid)id!) { }

    public Task<MusicBrainzReleaseAppends?> WithAppends(
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

        return Get<MusicBrainzReleaseAppends>(url: "release/" + id, query: queryParams, priority: priority);
    }

    public Task<MusicBrainzReleaseAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "inc"] = string.Join(separator: "+", value: appendices),
            [key: "fmt"] = "json",
        };

        return Get<MusicBrainzReleaseAppends>(url: "release/" + Id, query: queryParams, priority: priority);
    }

    public Task<MusicBrainzReleaseAppends?> WithAllAppends(Guid? id, bool? priority = false)
    {
        return WithAppends(
            id: (Guid)id!,
            appendices: new[]
            {
                "artists",
                "labels",
                "recordings",
                "release-groups",
                "media",
                "artist-credits",
                "discids",
                "puids",
                "isrcs",
                "artist-rels",
                "label-rels",
                "recording-rels",
                "release-rels",
                "release-group-rels",
                "url-rels",
                "work-rels",
                "recording-level-rels",
                "work-level-rels",
                "annotation",
                "aliases",
                "artist-credits",
                "collections",
                "genres",
                "tags",
            },
            priority: priority
        );
    }

    public Task<MusicBrainzReleaseAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(
            appendices:
            [
                "artists",
                "labels",
                "recordings",
                "release-groups",
                "media",
                "artist-credits",
                "discids",
                "puids",
                "isrcs",
                "artist-rels",
                "label-rels",
                "recording-rels",
                "release-rels",
                "release-group-rels",
                "url-rels",
                "work-rels",
                "recording-level-rels",
                "work-level-rels",
                "annotation",
                "aliases",
                "artist-credits",
                "collections",
                "genres",
                "tags",
            ],
            priority: priority
        );
    }

    public Task<MusicBrainzReleaseSearchResponse?> SearchReleases(
        string query,
        bool? priority = false
    )
    {
        Dictionary<string, string?>? queryParams = new()
        {
            [key: "query"] = query,
            [key: "inc"] = "recordings",
            [key: "fmt"] = "json",
        };
        return Get<MusicBrainzReleaseSearchResponse>(url: $"release", query: queryParams, priority: priority);
    }
}
