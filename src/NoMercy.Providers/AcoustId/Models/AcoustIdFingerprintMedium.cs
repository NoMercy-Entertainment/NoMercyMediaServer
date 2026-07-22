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

public class AcoustIdFingerprintMedium
{
    [JsonProperty(propertyName: "format")]
    public string? Format { get; set; }

    [JsonProperty(propertyName: "position")]
    public int? Position { get; set; }

    [JsonProperty(propertyName: "track_count")]
    public int? TrackCount { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public AcoustIdFingerprintTrack[] Tracks { get; set; } = [];
}
