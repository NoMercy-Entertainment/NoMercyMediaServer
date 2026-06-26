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

namespace NoMercy.Providers.MusicBrainz.Models;

public class Alias : MusicBrainzLifeSpan
{
    [JsonProperty("locale")]
    public string? Locale { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("primary")]
    public bool? Primary { get; set; }

    [JsonProperty("sort-name")]
    public string SortName { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("type-id")]
    public Guid? TypeId { get; set; }
}
