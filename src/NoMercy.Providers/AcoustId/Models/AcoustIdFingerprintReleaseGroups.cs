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

namespace NoMercy.Providers.AcoustId.Models;

public class AcoustIdFingerprintReleaseGroups
{
    [JsonProperty(propertyName: "artists")]
    public AcoustIdFingerprintArtist[] Artists { get; set; } = [];

    [JsonProperty(propertyName: "country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty(propertyName: "date")]
    public AcoustIdFingerprintDate? Date { get; set; }

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "medium_count")]
    public int? MediumCount { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "track_count")]
    public int? TrackCount { get; set; } = 0;

    [JsonProperty(propertyName: "mediums")]
    public AcoustIdFingerprintMedium[] Mediums { get; set; } = [];

    [JsonProperty(propertyName: "releaseevents")]
    public AcoustIdFingerprintReleaseEvent[] Releaseevents { get; set; } = [];
}
