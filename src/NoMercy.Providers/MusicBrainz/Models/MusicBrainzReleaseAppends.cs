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

public class MusicBrainzReleaseAppends : MusicBrainzRelease
{
    // [JsonProperty("aliases")] public object[] Aliases { get; set; }
    // [JsonProperty("annotation")] public object Annotation { get; set; }

    // [JsonProperty("asin")] public object Asin { get; set; }
    [JsonProperty(propertyName: "collections")]
    public Collection[] Collections { get; set; } = [];

    [JsonProperty(propertyName: "cover-art-archive")]
    public CoverArtArchive CoverArtArchive { get; set; } = new();

    [JsonProperty(propertyName: "label-info")]
    public LabelInfo[] LabelInfo { get; set; } = [];

    [JsonProperty(propertyName: "relations")]
    public MusicBrainzWorkRelation[] Relations { get; set; } = [];

    [JsonProperty(propertyName: "tags")]
    public MusicBrainzTag[] Tags { get; set; } = [];
}
