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

namespace NoMercy.Monitoring;

public class Gpu
{
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "d3d")]
    public double D3D { get; set; }

    [JsonProperty(propertyName: "decode")]
    public double Decode { get; set; }

    [JsonProperty(propertyName: "core")]
    public double Core { get; set; }

    [JsonProperty(propertyName: "memory")]
    public double Memory { get; set; }

    [JsonProperty(propertyName: "encode")]
    public double Encode { get; set; }

    [JsonProperty(propertyName: "power")]
    public double Power { get; set; }

    [JsonProperty(propertyName: "identifier")]
    internal string Identifier { get; set; } = string.Empty;

    // TryParse rather than Parse: an empty/default Identifier ("") splits into a
    // single empty-string segment (not null), so the "?? "0"" fallback never
    // fires and int.Parse("") throws FormatException. Every real provider sets
    // Identifier before Index is read, but a defensively-constructed Gpu (or a
    // future caller) must not crash on a missing/malformed identifier.
    [JsonProperty(propertyName: "index")]
    public int Index =>
        int.TryParse(s: Identifier.Split(separator: '/').LastOrDefault(), result: out int index) ? index : 0;
}
