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

public class Cpu
{
    [JsonProperty(propertyName: "total")]
    public double Total { get; set; }

    [JsonProperty(propertyName: "max")]
    public double Max { get; set; }

    [JsonProperty(propertyName: "core")]
    public List<Core> Core { get; set; } = [];
}
