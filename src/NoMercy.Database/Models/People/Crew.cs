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

namespace NoMercy.Database.Models.People;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(CreditId), additionalPropertyNames: [nameof(MovieId), nameof(JobId)], IsUnique = true)]
[Index(propertyName: nameof(CreditId), additionalPropertyNames: [nameof(TvId), nameof(JobId)], IsUnique = true)]
[Index(propertyName: nameof(CreditId), additionalPropertyNames: [nameof(SeasonId), nameof(JobId)], IsUnique = true)]
[Index(propertyName: nameof(CreditId), additionalPropertyNames: [nameof(EpisodeId), nameof(JobId)], IsUnique = true)]
[Index(propertyName: nameof(CreditId))]
[Index(propertyName: nameof(PersonId))]
[Index(propertyName: nameof(JobId), IsUnique = false)]
// The single-column owner-FK indexes (MovieId, TvId, SeasonId, EpisodeId) are declared
// in MediaContext.ConfigureCreditForeignKeyIndexes as partial indexes (WHERE col IS NOT
// NULL). Each crew credit belongs to exactly one owner, so every FK column is NULL on
// almost every row; a plain index over it is non-selective and the planner full-scans.
public class Crew
{
    [Key]
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "credit_id")]
    public string? CreditId { get; set; }

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty(propertyName: "season_id")]
    public int? SeasonId { get; set; }
    public Season? Season { get; set; }

    [JsonProperty(propertyName: "episode_id")]
    public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    [JsonProperty(propertyName: "person_id")]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    [JsonProperty(propertyName: "job_id")]
    public int? JobId { get; set; }
    public Job Job { get; set; } = null!;
}
