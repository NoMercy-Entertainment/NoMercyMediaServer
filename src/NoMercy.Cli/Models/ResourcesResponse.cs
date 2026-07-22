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

namespace NoMercy.Cli.Models;

internal class ResourcesResponse
{
    [JsonProperty(propertyName: "cpu")]
    public CpuInfo Cpu { get; set; } = new();

    [JsonProperty(propertyName: "gpu")]
    public List<GpuInfo> Gpu { get; set; } = [];

    [JsonProperty(propertyName: "memory")]
    public MemoryInfo Memory { get; set; } = new();

    [JsonProperty(propertyName: "storage")]
    public List<StorageInfo> Storage { get; set; } = [];
}

internal class CpuInfo
{
    [JsonProperty(propertyName: "total")]
    public double Total { get; set; }

    [JsonProperty(propertyName: "max")]
    public double Max { get; set; }
}

internal class GpuInfo
{
    [JsonProperty(propertyName: "core")]
    public double Core { get; set; }

    [JsonProperty(propertyName: "memory")]
    public double Memory { get; set; }

    [JsonProperty(propertyName: "encode")]
    public double Encode { get; set; }

    [JsonProperty(propertyName: "decode")]
    public double Decode { get; set; }

    [JsonProperty(propertyName: "index")]
    public int Index { get; set; }
}

internal class MemoryInfo
{
    [JsonProperty(propertyName: "available")]
    public double Available { get; set; }

    [JsonProperty(propertyName: "use")]
    public double Use { get; set; }

    [JsonProperty(propertyName: "total")]
    public double Total { get; set; }

    [JsonProperty(propertyName: "percentage")]
    public double Percentage { get; set; }
}

internal class StorageInfo
{
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "total")]
    public double Total { get; set; }

    [JsonProperty(propertyName: "available")]
    public double Available { get; set; }

    [JsonProperty(propertyName: "percentage")]
    public double Percentage { get; set; }
}
