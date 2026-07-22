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
using NoMercy.Providers.MusixMatch.Converters;

namespace NoMercy.Providers.MusixMatch.Models;

public class TrackLyricsGetMessage
{
    [JsonProperty(propertyName: "header")]
    public TrackLyricsGetMessageHeader Header { get; set; } = new();

    [JsonProperty(propertyName: "body")]
    [JsonConverter(converterType: typeof(ObjectOrEmptyArrayConverter<TrackLyricsGetMessagedBody>))]
    public TrackLyricsGetMessagedBody? Body { get; set; }
}
