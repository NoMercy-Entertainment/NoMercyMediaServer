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

namespace NoMercy.Database.Models.Media;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(TvId), additionalPropertyNames: [nameof(Iso6391), nameof(Iso31661)], IsUnique = true)]
[Index(propertyName: nameof(SeasonId), additionalPropertyNames: [nameof(Iso6391), nameof(Iso31661)], IsUnique = true)]
[Index(propertyName: nameof(EpisodeId), additionalPropertyNames: [nameof(Iso6391), nameof(Iso31661)], IsUnique = true)]
[Index(propertyName: nameof(MovieId), additionalPropertyNames: [nameof(Iso6391), nameof(Iso31661)], IsUnique = true)]
[Index(propertyName: nameof(CollectionId), additionalPropertyNames: [nameof(Iso6391), nameof(Iso31661)], IsUnique = true)]
[Index(propertyName: nameof(PersonId), additionalPropertyNames: [nameof(Iso6391), nameof(Iso31661)], IsUnique = true)]
[Index(propertyName: nameof(ReleaseGroupId), additionalPropertyNames: nameof(Iso31661), IsUnique = true)]
[Index(propertyName: nameof(ArtistId), additionalPropertyNames: nameof(Iso31661), IsUnique = true)]
[Index(propertyName: nameof(AlbumId), additionalPropertyNames: nameof(Iso31661), IsUnique = true)]
[Index(propertyName: nameof(GenreId), additionalPropertyNames: nameof(Iso6391), IsUnique = true)]
[Index(propertyName: nameof(TvId))]
[Index(propertyName: nameof(SeasonId))]
[Index(propertyName: nameof(EpisodeId))]
[Index(propertyName: nameof(MovieId))]
[Index(propertyName: nameof(CollectionId))]
[Index(propertyName: nameof(PersonId))]
[Index(propertyName: nameof(ReleaseGroupId))]
[Index(propertyName: nameof(ArtistId))]
[Index(propertyName: nameof(AlbumId))]
[Index(propertyName: nameof(GenreId))]
public class Translation : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "iso_3166_1")]
    public string? Iso31661 { get; set; }

    [JsonProperty(propertyName: "iso_639_1")]
    public string? Iso6391 { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "english_name")]
    public string? EnglishName { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "homepage")]
    public string? Homepage { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "biography")]
    public string? Biography { get; set; }

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

    [JsonProperty(propertyName: "collection_id")]
    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    [JsonProperty(propertyName: "person_id")]
    public int? PersonId { get; set; }
    public Person? People { get; set; }

    [JsonProperty(propertyName: "release_group_id")]
    public Guid? ReleaseGroupId { get; set; }
    public ReleaseGroup? ReleaseGroup { get; set; }

    [JsonProperty(propertyName: "artist_id")]
    public Guid? ArtistId { get; set; }
    public Artist? Artist { get; set; }

    [JsonProperty(propertyName: "release_id")]
    public Guid? AlbumId { get; set; }
    public Album? Album { get; set; }

    [JsonProperty(propertyName: "genre_id")]
    public int? GenreId { get; set; }
    public Genre? Genre { get; set; }
}
