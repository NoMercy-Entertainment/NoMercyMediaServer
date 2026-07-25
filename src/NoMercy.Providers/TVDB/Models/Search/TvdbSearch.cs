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

namespace NoMercy.Providers.TVDB.Models.Search;

public class TvdbSearchResponse : TvdbResponse<TvdbSearchResult[]> { }

public class TvdbSearchResult
{
    [JsonProperty("objectID")]
    public string ObjectId { get; set; } = string.Empty;

    [JsonProperty("aliases")]
    public string[]? Aliases { get; set; }

    [JsonProperty("country")]
    public string? Country { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("image_url")]
    public Uri? ImageUrl { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("first_air_time")]
    public string? FirstAirTime { get; set; }

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("primary_language")]
    public string? PrimaryLanguage { get; set; }

    [JsonProperty("primary_type")]
    public string? PrimaryType { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("tvdb_id")]
    public string? TvdbId { get; set; }

    [JsonProperty("year")]
    public string? Year { get; set; }

    [JsonProperty("slug")]
    public string? Slug { get; set; }

    [JsonProperty("overviews")]
    public Dictionary<string, string>? Overviews { get; set; }

    [JsonProperty("translations")]
    public Dictionary<string, string>? Translations { get; set; }

    [JsonProperty("network")]
    public string? Network { get; set; }

    [JsonProperty("remote_ids")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty("director")]
    public string? Director { get; set; }

    [JsonProperty("studios")]
    public string[]? Studios { get; set; }

    [JsonProperty("genres")]
    public string[]? Genres { get; set; }

    [JsonProperty("companies")]
    public string[]? Companies { get; set; }

    [JsonProperty("companyType")]
    public string? CompanyType { get; set; }

    [JsonProperty("officialList")]
    public string? OfficialList { get; set; }

    [JsonProperty("posters")]
    public string[]? Posters { get; set; }

    [JsonProperty("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }
}
