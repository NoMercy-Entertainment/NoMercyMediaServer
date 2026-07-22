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

public class MusicBrainzRecordingClient : MusicBrainzBaseClient
{
    public MusicBrainzRecordingClient(Guid? id, string[]? appendices = null)
        : base(id: (Guid)id!) { }

    public MusicBrainzRecordingClient()
        : base() { }

    public Task<MusicBrainzRecordingAppends?> WithAppends(
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

        return Get<MusicBrainzRecordingAppends>(url: "recording/" + id, query: queryParams, priority: priority);
    }

    public Task<MusicBrainzRecordingAppends?> WithAppends(
        string[] appendices,
        bool? priority = false
    )
    {
        Dictionary<string, string?>? queryParams = new()
        {
            [key: "inc"] = string.Join(separator: "+", value: appendices),
            [key: "fmt"] = "json",
        };

        return Get<MusicBrainzRecordingAppends>(url: "recording/" + Id, query: queryParams, priority: priority);
    }

    public Task<MusicBrainzRecordingAppends?> WithAllAppends(Guid? id, bool? priority = false)
    {
        return WithAppends(
            id: (Guid)id!,
            appendices: ["artist-credits", "artists", "releases", "tags", "genres"],
            priority: priority
        );
    }

    public Task<MusicBrainzRecordingAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends(appendices: ["artist-credits", "artists", "releases", "tags", "genres"], priority: priority);
    }

    public Task<MusicBrainzRecordingAppends?> SearchRecordings(string query, bool? priority = false)
    {
        Dictionary<string, string?>? queryParams = new()
        {
            [key: "query"] = query,
            [key: "inc"] = "releases",
            [key: "fmt"] = "json",
        };
        return Get<MusicBrainzRecordingAppends>(url: $"recording", query: queryParams, priority: priority);
    }

    public Task<MusicBrainzSearchResponse?> SearchRecordingsDynamic(
        string query,
        bool? priority = false
    )
    {
        Dictionary<string, string?>? queryParams = new()
        {
            [key: "query"] = query,
            [key: "inc"] = "releases",
            [key: "fmt"] = "json",
        };
        return Get<MusicBrainzSearchResponse>(url: $"recording", query: queryParams, priority: priority);
    }
}
