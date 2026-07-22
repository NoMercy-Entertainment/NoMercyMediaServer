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
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(TvId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(SeasonId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(EpisodeId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(MovieId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(CollectionId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(PersonId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(CastCreditId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(CrewCreditId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(ArtistId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(AlbumId), IsUnique = true)]
[Index(propertyName: nameof(FilePath), additionalPropertyNames: nameof(TrackId), IsUnique = true)]
[Index(propertyName: nameof(FilePath))]
// The single-column owner-FK indexes (TvId, SeasonId, EpisodeId, MovieId,
// CollectionId, PersonId, CastCreditId, CrewCreditId, ArtistId, AlbumId, TrackId)
// are declared in MediaContext.ConfigureImageForeignKeyIndexes as partial indexes
// (WHERE col IS NOT NULL). Each image has exactly one owner, so every FK column is
// NULL on almost every row; a plain index over it is non-selective and the planner
// full-scans. Filtering to non-NULL rows keeps each index small and seekable.
[Index(propertyName: nameof(Type), additionalPropertyNames: nameof(Iso6391))]
[Index(propertyName: nameof(MovieId), additionalPropertyNames: nameof(Type))]
[Index(propertyName: nameof(TvId), additionalPropertyNames: nameof(Type))]
[Index(propertyName: nameof(CollectionId), additionalPropertyNames: nameof(Type))]
public class Image : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "aspect_ratio")]
    public double AspectRatio { get; set; }

    [JsonProperty(propertyName: "file_path")]
    public string FilePath { get; set; } = null!;

    [JsonProperty(propertyName: "file_type")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "height")]
    public int? Height { get; set; }

    [JsonProperty(propertyName: "iso_639_1")]
    public string? Iso6391 { get; set; }

    [JsonProperty(propertyName: "site")]
    public string? Site { get; set; }

    [JsonProperty(propertyName: "size")]
    public int? Size { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = null!;

    [JsonProperty(propertyName: "vote_average")]
    public double? VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int? VoteCount { get; set; }

    [JsonProperty(propertyName: "width")]
    public int? Width { get; set; }

    [JsonProperty(propertyName: "cast_credit_id")]
    public string? CastCreditId { get; set; }

    [JsonIgnore]
    public int? CastId { get; set; }
    public virtual Cast? Cast { get; set; }

    [JsonProperty(propertyName: "crew_credit_id")]
    public string? CrewCreditId { get; set; }

    [JsonIgnore]
    public int? CrewId { get; set; }
    public virtual Crew? Crew { get; set; }

    [JsonProperty(propertyName: "person_id")]
    public int? PersonId { get; set; }
    public virtual Person? Person { get; set; }

    [JsonProperty(propertyName: "artist_id")]
    public Guid? ArtistId { get; set; }
    public virtual Artist? Artist { get; set; }

    [JsonProperty(propertyName: "album_id")]
    public Guid? AlbumId { get; set; }
    public virtual Album? Album { get; set; }

    [JsonProperty(propertyName: "track_id")]
    public Guid? TrackId { get; set; }
    public virtual Track? Track { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int? TvId { get; set; }
    public virtual Tv? Tv { get; set; }

    [JsonProperty(propertyName: "season_id")]
    public int? SeasonId { get; set; }
    public virtual Season? Season { get; set; }

    [JsonProperty(propertyName: "episode_id")]
    public int? EpisodeId { get; set; }
    public virtual Episode? Episode { get; set; }

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }
    public virtual Movie? Movie { get; set; }

    [JsonProperty(propertyName: "collection_id")]
    public int? CollectionId { get; set; }
    public virtual Collection? Collection { get; set; }
}
