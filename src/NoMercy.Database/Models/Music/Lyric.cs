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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Database.Models.Music;

public class Lyric
{
    [JsonProperty(propertyName: "text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty(propertyName: "time")]
    public LineTime Time { get; set; } = new();

    [JsonProperty(propertyName: "rtl")]
    public bool Rtl => Text.GetTextDirection() == StringExtensions.TextDirection.RTL;

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
