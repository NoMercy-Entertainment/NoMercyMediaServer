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

public class MusicBrainzArtistDetails : MusicBrainzArtist
{
    [JsonProperty("isnis")]
    public string[] Isnis { get; set; } = [];

    [JsonProperty("end_area")]
    public object? ArtistAppendsEndArea { get; set; }

    [JsonProperty("gender-id")]
    public Guid? GenderId { get; set; }

    [JsonProperty("area")]
    public MusicBrainzArea? MusicBrainzArea { get; set; }

    [JsonProperty("country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty("works")]
    public MusicBrainzWork[] Works { get; set; } = [];

    [JsonProperty("releases")]
    public MusicBrainzRelease[] Releases { get; set; } = [];

    [JsonProperty("release-groups")]
    public MusicBrainzReleaseGroup[] ReleaseGroups { get; set; } = [];

    [JsonProperty("end-area")]
    public MusicBrainzArea? EndArea { get; set; }

    [JsonProperty("life-span")]
    public MusicBrainzLifeSpan? LifeSpan { get; set; }

    [JsonProperty("begin-area")]
    public MusicBrainzArea? BeginArea { get; set; }

    [JsonProperty("ipis")]
    public string[] Ipis { get; set; } = [];
}
