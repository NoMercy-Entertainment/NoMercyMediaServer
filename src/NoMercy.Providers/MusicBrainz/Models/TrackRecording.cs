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

public class TrackRecording
{
    [JsonProperty(propertyName: "aliases")]
    public Alias[] Aliases { get; set; } = [];

    [JsonProperty(propertyName: "artist-credit")]
    public RecordingArtistCredit[] ArtistCredit { get; set; } = [];

    [JsonProperty(propertyName: "disambiguation")]
    public string Disambiguation { get; set; } = string.Empty;

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
        set => _firstReleaseDate = value.ToString().OrEmpty();
    }

    [JsonProperty(propertyName: "genres")]
    public MusicBrainzGenreDetails[] Genres { get; set; } = [];

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "isrcs")]
    public string[] Isrcs { get; set; } = [];

    [JsonProperty(propertyName: "length")]
    public int? Length { get; set; }

    [JsonProperty(propertyName: "relations")]
    public RecordingRelation[] Relations { get; set; } = [];

    [JsonProperty(propertyName: "tags")]
    public MusicBrainzTag[] Tags { get; set; } = [];

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "video")]
    public bool Video { get; set; }
}
