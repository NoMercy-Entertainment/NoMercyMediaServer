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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(TvId))]
[Index(propertyName: nameof(Title))]
[Index(propertyName: nameof(SeasonNumber))]
[Index(propertyName: nameof(AirDate))]
public class Season : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "air_date")]
    public DateTime? AirDate { get; set; }

    [JsonProperty(propertyName: "episode_count")]
    public int EpisodeCount { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "poster_path")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "season_number")]
    public int SeasonNumber { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    [JsonProperty(propertyName: "episodes")]
    public ICollection<Episode> Episodes { get; set; } = [];

    [JsonProperty(propertyName: "casts")]
    public ICollection<Cast> Cast { get; set; } = [];

    [JsonProperty(propertyName: "crews")]
    public ICollection<Crew> Crew { get; set; } = [];

    [JsonProperty(propertyName: "medias")]
    public ICollection<Media.Media> Medias { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ICollection<Image> Images { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public ICollection<Translation> Translations { get; set; } = [];
}
