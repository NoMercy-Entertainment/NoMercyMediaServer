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

namespace NoMercy.Providers.NoMercy.Models.Specials;

public class Special
{
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("poster")]
    public string? Poster { get; set; }

    [JsonProperty("logo")]
    public string? Logo { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty("creator")]
    public string? Creator { get; set; }

    [JsonProperty("overview")]
    public string? Overview { get; set; }
}
