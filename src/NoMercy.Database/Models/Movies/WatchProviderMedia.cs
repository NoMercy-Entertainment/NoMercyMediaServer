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

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Movies;

[PrimaryKey(propertyName: nameof(Id))]
[Index(
    propertyName: nameof(WatchProviderId), additionalPropertyNames: [nameof(CountryCode), nameof(ProviderType), nameof(MovieId), nameof(TvId)],
    IsUnique = true
)]
public class WatchProviderMedia : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "provider_id")]
    public int WatchProviderId { get; set; }

    [JsonProperty(propertyName: "country_code")]
    public string CountryCode { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string ProviderType { get; set; } = string.Empty; // "flatrate", "buy", "rent", "ads", "free"

    [JsonProperty(propertyName: "link")]
    public string? Link { get; set; }

    [JsonProperty(propertyName: "watch_provider")]
    public WatchProvider WatchProvider { get; set; } = null!;

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }

    [JsonProperty(propertyName: "movie")]
    public Movie? Movie { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int? TvId { get; set; }

    [JsonProperty(propertyName: "tv")]
    public Tv? Tv { get; set; }
}
