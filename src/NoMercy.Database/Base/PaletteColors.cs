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

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace NoMercy.Database;

public class PaletteColors
{
    [JsonProperty("dominant", NullValueHandling = NullValueHandling.Ignore)]
    public string Dominant { get; set; }

    [JsonProperty("primary", NullValueHandling = NullValueHandling.Ignore)]
    public string Primary { get; set; }

    [JsonProperty("lightVibrant", NullValueHandling = NullValueHandling.Ignore)]
    public string LightVibrant { get; set; }

    [JsonProperty("darkVibrant", NullValueHandling = NullValueHandling.Ignore)]
    public string DarkVibrant { get; set; }

    [JsonProperty("lightMuted", NullValueHandling = NullValueHandling.Ignore)]
    public string LightMuted { get; set; }

    [JsonProperty("darkMuted", NullValueHandling = NullValueHandling.Ignore)]
    public string DarkMuted { get; set; }
}
