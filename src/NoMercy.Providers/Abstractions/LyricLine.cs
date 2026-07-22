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

namespace NoMercy.Providers.Abstractions;

/// <summary>
/// Provider-neutral timed lyric line. Mirrors the wire shape (and JSON property
/// names) the player expects, so any lyric source maps onto a single DTO instead
/// of leaking a provider-specific type (previously a provider-specific lyric type)
/// across the codebase.
/// </summary>
public class LyricLine
{
    [JsonProperty(propertyName: "text")]
    public string Text = string.Empty;

    [JsonProperty(propertyName: "time")]
    public LineTime Time = new();

    public class LineTime
    {
        [JsonProperty(propertyName: "total")]
        public double Total;

        [JsonProperty(propertyName: "minutes")]
        public int Minutes;

        [JsonProperty(propertyName: "seconds")]
        public int Seconds;

        [JsonProperty(propertyName: "hundredths")]
        public int Hundredths;
    }
}
