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

namespace NoMercy.Api.Hubs.Shared;

public class Actions
{
    [JsonProperty(propertyName: "disallows")]
    public Disallows Disallows { get; set; } = null!;
}

public class Disallows
{
    [JsonProperty(propertyName: "previous")]
    public bool Previous { get; set; }

    [JsonProperty(propertyName: "next")]
    public bool Next { get; set; }

    [JsonProperty(propertyName: "resuming")]
    public bool Resuming { get; set; }

    [JsonProperty(propertyName: "pausing")]
    public bool Pausing { get; set; }

    [JsonProperty(propertyName: "toggling_repeat_context")]
    public bool TogglingRepeatContext { get; set; }

    [JsonProperty(propertyName: "toggling_repeat_track")]
    public bool TogglingRepeatTrack { get; set; }

    [JsonProperty(propertyName: "toggling_shuffle")]
    public bool TogglingShuffle { get; set; }

    [JsonProperty(propertyName: "seeking")]
    public bool Seeking { get; set; }

    [JsonProperty(propertyName: "stopping")]
    public bool Stopping { get; set; }

    [JsonProperty(propertyName: "muting")]
    public bool Muting { get; set; }
}
