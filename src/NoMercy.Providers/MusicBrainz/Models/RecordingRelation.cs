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

public class RecordingRelation : MusicBrainzLifeSpan
{
    [JsonProperty(propertyName: "attribute-ids")]
    public Dictionary<string, Guid> AttributeIds { get; set; } = new();

    [JsonProperty(propertyName: "attribute-values")]
    public MusicBrainzAttributeValues MusicBrainzAttributeValues { get; set; } = new();

    [JsonProperty(propertyName: "attributes")]
    public string[] Attributes { get; set; } = [];

    [JsonProperty(propertyName: "source-credit")]
    public string SourceCredit { get; set; } = string.Empty;

    [JsonProperty(propertyName: "target-credit")]
    public string TargetCredit { get; set; } = string.Empty;

    [JsonProperty(propertyName: "target-type")]
    public string TargetType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type-id")]
    public Guid? TypeId { get; set; }

    [JsonProperty(propertyName: "artist")]
    public PurpleArtist Artist { get; set; } = new();

    [JsonProperty(propertyName: "attribute-credits")]
    public AttributeCredits AttributeCredits { get; set; } = new();

    [JsonProperty(propertyName: "label")]
    public PurpleArtist Label { get; set; } = new();

    [JsonProperty(propertyName: "work")]
    public MusicBrainzWork MusicBrainzWork { get; set; } = new();

    [JsonProperty(propertyName: "recording")]
    public RelationRecording Recording { get; set; } = new();
}
