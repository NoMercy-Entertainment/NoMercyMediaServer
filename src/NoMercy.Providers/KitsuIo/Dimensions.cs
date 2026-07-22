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

namespace NoMercy.Providers.KitsuIo;

public class Dimensions
{
    [JsonProperty(propertyName: "tiny")]
    public Large? Tiny { get; set; }

    [JsonProperty(propertyName: "large?")]
    public Large? Large { get; set; }

    [JsonProperty(propertyName: "small")]
    public Large? Small { get; set; }

    [JsonProperty(propertyName: "medium")]
    public Large? Medium { get; set; }
}
