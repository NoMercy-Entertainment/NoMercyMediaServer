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

namespace NoMercy.Database;

public class VideoTrack
{
    [JsonProperty(propertyName: "file")]
    public string File { get; set; } = null!;

    [JsonProperty(propertyName: "kind")]
    public string Kind { get; set; } = null!;

    [JsonProperty(propertyName: "label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    [JsonProperty(propertyName: "language", NullValueHandling = NullValueHandling.Ignore)]
    public string? Language { get; set; }
}
