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
    [JsonProperty("artists")]
    public AcoustIdFingerprintArtist[] Artists { get; set; } = [];

    // AcoustID reports this in fractional seconds ("duration": 205.291), which no
    // int can hold: the bind failed, the global Newtonsoft error handler swallowed
    // it, and every duration comparison ran against 0.
    [JsonProperty("duration")]
    public double Duration { get; set; }

    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("releases")]
    public AcoustIdFingerprintReleaseGroups[]? Releases { get; set; } = [];

    [JsonProperty("sources")]
    public int Sources { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
}
