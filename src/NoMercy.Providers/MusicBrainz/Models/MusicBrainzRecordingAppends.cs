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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzRecordingAppends : MusicBrainzRecording
{
    // ReSharper disable once InconsistentNaming
    [JsonProperty(propertyName: "first-release-date")]
    private string? _firstReleaseDate { get; set; }

    public DateTime? FirstReleaseDate
    {
        get =>
            !string.IsNullOrWhiteSpace(value: _firstReleaseDate)
            && !string.IsNullOrEmpty(value: _firstReleaseDate)
            && _firstReleaseDate.TryParseToDateTime(dateTime: out DateTime dt)
                ? dt
                : null;
        set => _firstReleaseDate = value.ToString();
    }

    [JsonProperty(propertyName: "media")]
    public MusicBrainzMedia[] Media { get; set; } = [];

    [JsonProperty(propertyName: "tags")]
    public MusicBrainzTag[] Tags { get; set; } = [];

    [JsonProperty(propertyName: "releases")]
    public MusicBrainzRelease[] Releases { get; set; } = [];

    [JsonProperty(propertyName: "artist-credit")]
    public MusicBrainzArtistCredit[] ArtistCredit { get; set; } = [];
}

public class MusicBrainzSearchResponse
{
    [JsonProperty(propertyName: "created")]
    public DateTime Created { get; set; }

    [JsonProperty(propertyName: "count")]
    public int Count { get; set; }

    [JsonProperty(propertyName: "offset")]
    public int Offset { get; set; }

    [JsonProperty(propertyName: "recordings")]
    public List<MusicBrainzSearchRecording> Recordings { get; set; } = [];
}

public class MusicBrainzSearchRecording : MusicBrainzRecordingAppends
{
    [JsonProperty(propertyName: "score")]
    public int Score { get; set; }
}
