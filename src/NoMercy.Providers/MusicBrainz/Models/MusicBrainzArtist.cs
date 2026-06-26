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

public class MusicBrainzArtist
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type-id")]
    public Guid? TypeId { get; set; }

    [JsonProperty("disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty("sort-name")]
    public string SortName { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("tags")]
    public MusicBrainzTag[] Tags { get; set; } = [];

    [JsonProperty("genres")]
    public MusicBrainzGenreDetails[] Genres { get; set; } = [];

    [JsonProperty("iso-3166-1-codes")]
    public string[] Iso31661Codes { get; set; } = [];

    [JsonProperty("iso-3166-2-codes")]
    public string[] Iso31662Codes { get; set; } = [];
}
