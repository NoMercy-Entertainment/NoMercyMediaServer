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

namespace NoMercy.Providers.TVDB.Models.Shared;

public class TvdbResponse<T>
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "data")]
    public T Data { get; set; } = default!;

    [JsonProperty(propertyName: "links")]
    public TvdbLinks? Links { get; set; }
}

public class TvdbPaginatedResponse<T>
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "data")]
    public List<T> Data { get; set; } = [];

    [JsonProperty(propertyName: "links")]
    public TvdbLinks? Links { get; set; }
}

public class TvdbLinks
{
    [JsonProperty(propertyName: "prev")]
    public string? Prev { get; set; }

    [JsonProperty(propertyName: "self")]
    public string? Self { get; set; }

    [JsonProperty(propertyName: "next")]
    public string? Next { get; set; }

    [JsonProperty(propertyName: "total_items")]
    public int TotalItems { get; set; }

    [JsonProperty(propertyName: "page_size")]
    public int PageSize { get; set; }
}
