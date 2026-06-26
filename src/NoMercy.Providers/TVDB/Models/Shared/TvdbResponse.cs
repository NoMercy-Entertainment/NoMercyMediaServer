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
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("data")]
    public T Data { get; set; } = default!;

    [JsonProperty("links")]
    public TvdbLinks? Links { get; set; }
}

public class TvdbPaginatedResponse<T>
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("data")]
    public List<T> Data { get; set; } = [];

    [JsonProperty("links")]
    public TvdbLinks? Links { get; set; }
}

public class TvdbLinks
{
    [JsonProperty("prev")]
    public string? Prev { get; set; }

    [JsonProperty("self")]
    public string? Self { get; set; }

    [JsonProperty("next")]
    public string? Next { get; set; }

    [JsonProperty("total_items")]
    public int TotalItems { get; set; }

    [JsonProperty("page_size")]
    public int PageSize { get; set; }
}
