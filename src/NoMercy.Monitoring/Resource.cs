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

public class Resource
{
    [JsonProperty("cpu")]
    public Cpu Cpu { get; set; } = new();

    // ReSharper disable once InconsistentNaming
    internal Dictionary<string, Gpu> _gpu { get; set; } = [];

    [JsonProperty("memory")]
    public Memory Memory { get; set; } = new();

    [JsonProperty("gpu")]
    public List<Gpu> Gpu => _gpu.Values.ToList();
}
