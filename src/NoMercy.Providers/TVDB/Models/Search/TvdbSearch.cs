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
    [JsonProperty(propertyName: "objectID")]
    public string ObjectId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "aliases")]
    public string[]? Aliases { get; set; }

    [JsonProperty(propertyName: "country")]
    public string? Country { get; set; }

    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "image_url")]
    public Uri? ImageUrl { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "first_air_time")]
    public string? FirstAirTime { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "primary_language")]
    public string? PrimaryLanguage { get; set; }

    [JsonProperty(propertyName: "primary_type")]
    public string? PrimaryType { get; set; }

    [JsonProperty(propertyName: "status")]
    public string? Status { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "tvdb_id")]
    public string? TvdbId { get; set; }

    [JsonProperty(propertyName: "year")]
    public string? Year { get; set; }

    [JsonProperty(propertyName: "slug")]
    public string? Slug { get; set; }

    [JsonProperty(propertyName: "overviews")]
    public Dictionary<string, string>? Overviews { get; set; }

    [JsonProperty(propertyName: "translations")]
    public Dictionary<string, string>? Translations { get; set; }

    [JsonProperty(propertyName: "network")]
    public string? Network { get; set; }

    [JsonProperty(propertyName: "remote_ids")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty(propertyName: "director")]
    public string? Director { get; set; }

    [JsonProperty(propertyName: "studios")]
    public string[]? Studios { get; set; }

    [JsonProperty(propertyName: "genres")]
    public string[]? Genres { get; set; }

    [JsonProperty(propertyName: "companies")]
    public string[]? Companies { get; set; }

    [JsonProperty(propertyName: "companyType")]
    public string? CompanyType { get; set; }

    [JsonProperty(propertyName: "officialList")]
    public string? OfficialList { get; set; }

    [JsonProperty(propertyName: "posters")]
    public string[]? Posters { get; set; }

    [JsonProperty(propertyName: "thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }
}
