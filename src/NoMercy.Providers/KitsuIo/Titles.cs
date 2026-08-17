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

public class Titles
{
    [JsonProperty("en")]
    public string? En { get; set; }

    [JsonProperty("en_us")]
    public string? EnUs { get; set; }

    [JsonProperty("en_jp")]
    public string? EnJp { get; set; }

    [JsonProperty("ja_jp")]
    public string? JaJp { get; set; }

    [JsonProperty("th_th")]
    public string? ThTh { get; set; }
}
