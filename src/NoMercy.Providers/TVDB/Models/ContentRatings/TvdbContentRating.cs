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
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty(propertyName: "country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty(propertyName: "contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "order")]
    public int Order { get; set; }

    [JsonProperty(propertyName: "fullName")]
    public string FullName { get; set; } = string.Empty;
}
