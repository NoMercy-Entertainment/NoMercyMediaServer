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

public class MusicBrainzRelease
{
    [JsonProperty(propertyName: "barcode")]
    public string Barcode { get; set; } = string.Empty;

    [JsonProperty(propertyName: "country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty(propertyName: "score")]
    public int? Score { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "genres")]
    public MusicBrainzGenreDetails[] Genres { get; set; } = [];

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "media")]
    public MusicBrainzMedia[] Media { get; set; } = [];

    [JsonProperty(propertyName: "packaging")]
    public string Packaging { get; set; } = string.Empty;

    [JsonProperty(propertyName: "packaging-id")]
    public Guid? PackagingId { get; set; }

    [JsonProperty(propertyName: "quality")]
    public string Quality { get; set; } = string.Empty;

    [JsonProperty(propertyName: "release-events")]
    public ReleaseEvent[]? ReleaseEvents { get; set; } = [];

    [JsonProperty(propertyName: "release-group")]
    public MusicBrainzReleaseGroup MusicBrainzReleaseGroup { get; set; } = new();

    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "status-id")]
    public Guid? StatusId { get; set; }

    [JsonProperty(propertyName: "artist-credit")]
    public ReleaseArtistCredit[] ArtistCredit { get; set; } = [];

    [JsonProperty(propertyName: "text-representation")]
    public MusicBrainzTextRepresentation MusicBrainzTextRepresentation { get; set; } = new();

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "area")]
    public MusicBrainzArea MusicBrainzArea { get; set; } = new();

    // ReSharper disable once InconsistentNaming
    [JsonProperty(propertyName: "date")]
    private string _date { get; set; } = string.Empty;

    [JsonProperty(propertyName: "dateTime")]
    public DateTime? DateTime
    {
        get =>
            !string.IsNullOrWhiteSpace(value: _date)
            && !string.IsNullOrEmpty(value: _date)
            && _date.TryParseToDateTime(dateTime: out DateTime dt)
                ? dt
                : null;
        set => _date = value.ToString().OrEmpty();
    }
}
