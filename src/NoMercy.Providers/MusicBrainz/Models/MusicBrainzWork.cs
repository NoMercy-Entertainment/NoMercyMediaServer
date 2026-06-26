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

public class MusicBrainzWork
{
    [JsonProperty("attributes")]
    public object[] Attributes { get; set; } = [];

    [JsonProperty("disambiguation")]
    public string Disambiguation { get; set; } = string.Empty;

    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("iswcs")]
    public string[] Iswcs { get; set; } = [];

    [JsonProperty("language")]
    public string Language { get; set; } = string.Empty;

    [JsonProperty("languages")]
    public string[] Languages { get; set; } = [];

    [JsonProperty("relations")]
    public MusicBrainzWorkRelation[] Relations { get; set; } = [];

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("type-id")]
    public Guid? TypeId { get; set; }
}
