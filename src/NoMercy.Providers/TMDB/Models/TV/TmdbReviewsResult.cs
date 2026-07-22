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

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbReviewsResult
{
    [JsonProperty(propertyName: "author")]
    public string Author { get; set; } = string.Empty;

    [JsonProperty(propertyName: "author_details")]
    public TmdbAuthorDetails TmdbAuthorDetails { get; set; } = new();

    [JsonProperty(propertyName: "content")]
    public string Content { get; set; } = string.Empty;

    [JsonProperty(propertyName: "created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonProperty(propertyName: "url")]
    public Uri? Url { get; set; }
}
