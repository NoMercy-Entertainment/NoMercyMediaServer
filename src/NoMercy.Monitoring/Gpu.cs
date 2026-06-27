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
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("d3d")]
    public double D3D { get; set; }

    [JsonProperty("decode")]
    public double Decode { get; set; }

    [JsonProperty("core")]
    public double Core { get; set; }

    [JsonProperty("memory")]
    public double Memory { get; set; }

    [JsonProperty("encode")]
    public double Encode { get; set; }

    [JsonProperty("power")]
    public double Power { get; set; }

    [JsonProperty("identifier")]
    internal string Identifier { get; set; } = string.Empty;

    [JsonProperty("index")]
    public int Index => int.Parse(Identifier.Split('/').LastOrDefault() ?? "0");
}
