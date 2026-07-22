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

using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;

/// <summary>
/// Response from <c>GET /ws/2/discid/{id}</c> (exact TOC match) or
/// <c>GET /ws/2/discid/-?toc=…</c> (fuzzy TOC lookup).
///
/// Exact match: <c>releases</c> is populated directly on the root object.
/// Fuzzy match: the server returns the same shape — an array of releases
/// whose TOC structure matches the supplied toc= parameter.
/// </summary>
public class DiscIdLookupResponse
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "offset-count")]
    public int OffsetCount { get; set; }

    [JsonProperty(propertyName: "offsets")]
    public int[] Offsets { get; set; } = [];

    [JsonProperty(propertyName: "sectors")]
    public int Sectors { get; set; }

    [JsonProperty(propertyName: "releases")]
    public MusicBrainzReleaseAppends[] Releases { get; set; } = [];
}
