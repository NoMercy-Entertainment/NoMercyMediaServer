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

public class MusicBrainzLabel
{
    [JsonProperty(propertyName: "aliases")]
    public Alias[] Aliases { get; set; } = [];

    [JsonProperty(propertyName: "disambiguation")]
    public string Disambiguation { get; set; } = string.Empty;

    [JsonProperty(propertyName: "genres")]
    public MusicBrainzGenreDetails[] Genres { get; set; } = [];

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "label-code")]
    public string? LabelCode { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "sort-name")]
    public string SortName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tags")]
    public MusicBrainzTag[] Tags { get; set; } = [];

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type-id")]
    public Guid? TypeId { get; set; }
}
