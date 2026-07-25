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

namespace NoMercy.MediaProcessing.Files;

public record Streams
{
    [JsonProperty("video")]
    public IEnumerable<Video> Video { get; set; } = new List<Video>();

    [JsonProperty("audio")]
    public IEnumerable<Audio> Audio { get; set; } = new List<Audio>();

    [JsonProperty("subtitle")]
    public IEnumerable<Subtitle> Subtitle { get; set; } = new List<Subtitle>();
}
