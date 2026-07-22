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

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchMatcherTrackGetMessageHeader
{
    [JsonProperty(propertyName: "status_code")]
    public long StatusCode { get; set; }

    [JsonProperty(propertyName: "execute_time")]
    public double ExecuteTime { get; set; }

    [JsonProperty(propertyName: "confidence")]
    public long Confidence { get; set; }

    [JsonProperty(propertyName: "mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonProperty(propertyName: "cached")]
    public long Cached { get; set; }
}
