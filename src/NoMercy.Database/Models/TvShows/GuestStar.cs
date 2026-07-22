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
[Index(propertyName: nameof(CreditId), additionalPropertyNames: nameof(EpisodeId), IsUnique = true)]
[Index(propertyName: nameof(CreditId))]
[Index(propertyName: nameof(EpisodeId))]
[Index(propertyName: nameof(PersonId))]
public class GuestStar
{
    [Key]
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "credit_id")]
    public string? CreditId { get; set; }

    [JsonProperty(propertyName: "episode_id")]
    public int EpisodeId { get; set; }
    public Episode Episode { get; set; } = null!;

    [JsonProperty(propertyName: "person_id")]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
}
