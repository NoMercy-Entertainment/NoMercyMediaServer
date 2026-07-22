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

namespace NoMercy.Database.Models.Media;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(TvId), additionalPropertyNames: nameof(Src), IsUnique = true)]
[Index(propertyName: nameof(SeasonId), additionalPropertyNames: nameof(Src), IsUnique = true)]
[Index(propertyName: nameof(EpisodeId), additionalPropertyNames: nameof(Src), IsUnique = true)]
[Index(propertyName: nameof(MovieId), additionalPropertyNames: nameof(Src), IsUnique = true)]
[Index(propertyName: nameof(PersonId), additionalPropertyNames: nameof(Src), IsUnique = true)]
[Index(propertyName: nameof(VideoFileId), additionalPropertyNames: nameof(Src), IsUnique = true)]
[Index(propertyName: nameof(Type))]
[Index(propertyName: nameof(Site))]
[Index(propertyName: nameof(Name))]
public class Media : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [JsonProperty(propertyName: "iso_639_1")]
    public string? Iso6391 { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "site")]
    public string? Site { get; set; }

    [JsonProperty(propertyName: "size")]
    public int Size { get; set; }

    [JsonProperty(propertyName: "src")]
    public string Src { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty(propertyName: "season_id")]
    public int? SeasonId { get; set; }
    public Season? Season { get; set; }

    [JsonProperty(propertyName: "episode_id")]
    public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty(propertyName: "person_id")]
    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    [JsonProperty(propertyName: "video_file_id")]
    public Ulid? VideoFileId { get; set; }
    public VideoFile? VideoFile { get; set; }
}
