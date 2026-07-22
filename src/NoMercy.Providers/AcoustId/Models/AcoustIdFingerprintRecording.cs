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

public class AcoustIdFingerprintRecording
{
    [JsonProperty(propertyName: "artists")]
    public AcoustIdFingerprintArtist[] Artists { get; set; } = [];

    [JsonProperty(propertyName: "duration")]
    public int Duration { get; set; }

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "releases")]
    public AcoustIdFingerprintReleaseGroups[]? Releases { get; set; } = [];

    [JsonProperty(propertyName: "sources")]
    public int Sources { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;
}
