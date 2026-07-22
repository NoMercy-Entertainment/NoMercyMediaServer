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

using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

/// <summary>
/// MusicBrainz disc-lookup operations.
///
/// Exact lookup:  GET /ws/2/discid/{id}?inc=recordings+artist-credits+release-groups&amp;fmt=json
/// Fuzzy lookup:  GET /ws/2/discid/-?toc=…&amp;inc=recordings+artist-credits+release-groups&amp;fmt=json
///
/// The caller is responsible for building the <c>toc=</c> string (see
/// <c>AudioCdIdentifier.BuildTocString</c> in NoMercy.OpticalMedia). This
/// keeps NoMercy.Providers free of a circular dependency on NoMercy.OpticalMedia.
///
/// Both calls use the base client's rate-limited queue (1 req/s) and
/// file-backed response cache.
/// </summary>
public sealed class MusicBrainzDiscClient : MusicBrainzBaseClient
{
    private static readonly string[] DefaultIncludes =
    [
        "recordings",
        "artist-credits",
        "release-groups",
    ];

    public MusicBrainzDiscClient()
        : base() { }

    /// <summary>
    /// Exact disc-id lookup. Returns null when the disc id is not found (404).
    /// </summary>
    public Task<DiscIdLookupResponse?> LookupByDiscId(
        string discId,
        bool? priority = false,
        CancellationToken ct = default
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "inc"] = string.Join(separator: "+", value: DefaultIncludes),
            [key: "fmt"] = "json",
        };

        return Get<DiscIdLookupResponse>(url: $"discid/{discId}", query: queryParams, priority: priority);
    }

    /// <summary>
    /// Fuzzy TOC lookup using a pre-built <c>toc=</c> query string.
    /// The string format is: <c>firstTrack+lastTrack+leadOut+t1+t2…</c>
    /// where all offsets include the +150 pre-gap (as per the MusicBrainz spec).
    /// Build this string via <c>AudioCdIdentifier.BuildTocString(DiscToc)</c>.
    /// Returns null when the server returns no matches.
    /// </summary>
    public Task<DiscIdLookupResponse?> LookupByTocString(
        string tocString,
        bool? priority = false,
        CancellationToken ct = default
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            [key: "toc"] = tocString,
            [key: "inc"] = string.Join(separator: "+", value: DefaultIncludes),
            [key: "fmt"] = "json",
        };

        return Get<DiscIdLookupResponse>(url: "discid/-", query: queryParams, priority: priority);
    }
}
