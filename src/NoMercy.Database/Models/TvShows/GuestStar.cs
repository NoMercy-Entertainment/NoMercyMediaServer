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

[PrimaryKey(nameof(Id))]
[Index(nameof(CreditId), nameof(EpisodeId), IsUnique = true)]
[Index(nameof(CreditId))]
[Index(nameof(EpisodeId))]
[Index(nameof(PersonId))]
public class GuestStar
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("credit_id")]
    public string? CreditId { get; set; }

    [JsonProperty("episode_id")]
    public int EpisodeId { get; set; }
    public Episode Episode { get; set; } = null!;

    [JsonProperty("person_id")]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
}
