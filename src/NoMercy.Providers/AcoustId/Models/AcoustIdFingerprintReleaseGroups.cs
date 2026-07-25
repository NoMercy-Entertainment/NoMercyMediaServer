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
    [JsonProperty("artists")]
    public AcoustIdFingerprintArtist[] Artists { get; set; } = [];

    [JsonProperty("country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty("date")]
    public AcoustIdFingerprintDate? Date { get; set; }

    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("medium_count")]
    public int? MediumCount { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("track_count")]
    public int? TrackCount { get; set; } = 0;

    [JsonProperty("mediums")]
    public AcoustIdFingerprintMedium[] Mediums { get; set; } = [];

    [JsonProperty("releaseevents")]
    public AcoustIdFingerprintReleaseEvent[] Releaseevents { get; set; } = [];
}
