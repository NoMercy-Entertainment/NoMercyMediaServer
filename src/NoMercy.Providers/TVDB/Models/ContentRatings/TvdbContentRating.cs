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

namespace NoMercy.Providers.TVDB.Models.ContentRatings;

public class TvdbContentRatingsResponse : TvdbResponse<TvdbContentRating[]> { }

public class TvdbContentRating
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonProperty("order")]
    public int Order { get; set; }

    [JsonProperty("fullName")]
    public string FullName { get; set; } = string.Empty;
}
