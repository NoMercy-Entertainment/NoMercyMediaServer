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

public class MatcherTrackGetMessage
{
    [JsonProperty(propertyName: "header")]
    public MusixMatchMatcherTrackGetMessageHeader Header { get; set; } = new();

    [JsonProperty(propertyName: "body")]
    [JsonConverter(converterType: typeof(ObjectOrEmptyArrayConverter<MatcherTrackGetMessageBody>))]
    public MatcherTrackGetMessageBody Body { get; set; } = new();
}
