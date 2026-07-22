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

namespace NoMercy.Database.Models.People;

public class TmdbPersonExternalIds
{
    [JsonProperty(propertyName: "imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty(propertyName: "facebook_id")]
    public string? FacebookId { get; set; }

    [JsonProperty(propertyName: "freebase_mid")]
    public string? FreebaseMid { get; set; }

    [JsonProperty(propertyName: "freebase_id")]
    public string? FreebaseId { get; set; }

    [JsonProperty(propertyName: "twitter_id")]
    public string? TwitterId { get; set; }

    [JsonProperty(propertyName: "tvrage_id")]
    public string? TvRageId { get; set; }

    [JsonProperty(propertyName: "wikidata_id")]
    public string? WikipediaId { get; set; }

    [JsonProperty(propertyName: "instagram_id")]
    public string? InstagramId { get; set; }

    [JsonProperty(propertyName: "tiktok_id")]
    public string? TikTokId { get; set; }

    [JsonProperty(propertyName: "youtube_id")]
    public string? YoutubeId { get; set; }
}
