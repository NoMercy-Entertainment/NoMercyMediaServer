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

[PrimaryKey(nameof(Id))]
[Index(nameof(FilePath), nameof(TvId), IsUnique = true)]
[Index(nameof(FilePath), nameof(SeasonId), IsUnique = true)]
[Index(nameof(FilePath), nameof(EpisodeId), IsUnique = true)]
[Index(nameof(FilePath), nameof(MovieId), IsUnique = true)]
[Index(nameof(FilePath), nameof(CollectionId), IsUnique = true)]
[Index(nameof(FilePath), nameof(PersonId), IsUnique = true)]
[Index(nameof(FilePath), nameof(CastCreditId), IsUnique = true)]
[Index(nameof(FilePath), nameof(CrewCreditId), IsUnique = true)]
[Index(nameof(FilePath), nameof(ArtistId), IsUnique = true)]
[Index(nameof(FilePath), nameof(AlbumId), IsUnique = true)]
[Index(nameof(FilePath), nameof(TrackId), IsUnique = true)]
[Index(nameof(FilePath))]
// The single-column owner-FK indexes (TvId, SeasonId, EpisodeId, MovieId,
// CollectionId, PersonId, CastCreditId, CrewCreditId, ArtistId, AlbumId, TrackId)
// are declared in MediaContext.ConfigureImageForeignKeyIndexes as partial indexes
// (WHERE col IS NOT NULL). Each image has exactly one owner, so every FK column is
// NULL on almost every row; a plain index over it is non-selective and the planner
// full-scans. Filtering to non-NULL rows keeps each index small and seekable.
[Index(nameof(Type), nameof(Iso6391))]
[Index(nameof(MovieId), nameof(Type))]
[Index(nameof(TvId), nameof(Type))]
[Index(nameof(CollectionId), nameof(Type))]
public class Image : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("aspect_ratio")]
    public double AspectRatio { get; set; }

    [JsonProperty("file_path")]
    public string FilePath { get; set; } = null!;

    [JsonProperty("file_type")]
    public string? Name { get; set; }

    [JsonProperty("height")]
    public int? Height { get; set; }

    [JsonProperty("iso_639_1")]
    public string? Iso6391 { get; set; }

    [JsonProperty("site")]
    public string? Site { get; set; }

    [JsonProperty("size")]
    public int? Size { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = null!;

    [JsonProperty("vote_average")]
    public double? VoteAverage { get; set; }

    [JsonProperty("vote_count")]
    public int? VoteCount { get; set; }

    [JsonProperty("width")]
    public int? Width { get; set; }

    [JsonProperty("cast_credit_id")]
    public string? CastCreditId { get; set; }

    [JsonIgnore]
    public int? CastId { get; set; }
    public virtual Cast? Cast { get; set; }

    [JsonProperty("crew_credit_id")]
    public string? CrewCreditId { get; set; }

    [JsonIgnore]
    public int? CrewId { get; set; }
    public virtual Crew? Crew { get; set; }

    [JsonProperty("person_id")]
    public int? PersonId { get; set; }
    public virtual Person? Person { get; set; }

    [JsonProperty("artist_id")]
    public Guid? ArtistId { get; set; }
    public virtual Artist? Artist { get; set; }

    [JsonProperty("album_id")]
    public Guid? AlbumId { get; set; }
    public virtual Album? Album { get; set; }

    [JsonProperty("track_id")]
    public Guid? TrackId { get; set; }
    public virtual Track? Track { get; set; }

    [JsonProperty("tv_id")]
    public int? TvId { get; set; }
    public virtual Tv? Tv { get; set; }

    [JsonProperty("season_id")]
    public int? SeasonId { get; set; }
    public virtual Season? Season { get; set; }

    [JsonProperty("episode_id")]
    public int? EpisodeId { get; set; }
    public virtual Episode? Episode { get; set; }

    [JsonProperty("movie_id")]
    public int? MovieId { get; set; }
    public virtual Movie? Movie { get; set; }

    [JsonProperty("collection_id")]
    public int? CollectionId { get; set; }
    public virtual Collection? Collection { get; set; }
}
