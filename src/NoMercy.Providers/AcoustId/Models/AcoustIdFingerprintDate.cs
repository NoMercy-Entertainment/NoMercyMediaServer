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

namespace NoMercy.Providers.AcoustId.Models;

public class AcoustIdFingerprintDate
{
    [JsonProperty("day")]
    public int Day { get; set; }

    [JsonProperty("month")]
    public int Month { get; set; }

    [JsonProperty("year")]
    public int Year { get; set; }
}
