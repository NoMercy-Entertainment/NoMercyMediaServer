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
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Inspirations;

public class TvdbInspirationTypesResponse : TvdbResponse<TvdbInspirationType[]> { }

public class TvdbInspirationType
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty(propertyName: "reference_name")]
    public string ReferenceName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "url")]
    public Uri? Url { get; set; }
}

public class TvdbInspiration
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "type")]
    public int Type { get; set; }

    [JsonProperty(propertyName: "type_name")]
    public string TypeName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "url")]
    public string? Url { get; set; }
}
