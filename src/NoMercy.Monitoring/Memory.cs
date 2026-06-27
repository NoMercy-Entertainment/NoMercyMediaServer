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

public class Memory
{
    [JsonProperty("available")]
    public double Available { get; set; }

    [JsonProperty("use")]
    public double Use { get; set; }

    [JsonProperty("total")]
    public double Total { get; set; }

    [JsonProperty("percentage")]
    public double Percentage => Use / (Available + Use) * 100;
}
