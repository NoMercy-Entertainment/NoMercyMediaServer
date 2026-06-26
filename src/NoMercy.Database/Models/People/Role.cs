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

namespace NoMercy.Database.Models.People;

[PrimaryKey(nameof(Id))]
[Index(nameof(CreditId), IsUnique = true)]
[Index(nameof(GuestStarId), IsUnique = true)]
public class Role
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("character")]
    public string? Character { get; set; }

    [JsonProperty("episode_count")]
    public int EpisodeCount { get; set; }

    [JsonProperty("order")]
    public int? Order { get; set; } = 9999;

    [JsonProperty("credit_id")]
    public string? CreditId { get; set; }
    public Cast? Cast { get; set; }

    [JsonProperty("guest_star_id")]
    public int? GuestStarId { get; set; }
    public GuestStar? GuestStar { get; set; }

    // public Role(TmdbAggregatedCreditRole role)
    // {
    //     Character = role.Character;
    //     EpisodeCount = role.EpisodeCount;
    //     Order = role.Order;
    //     CreditId = role.CreditId;
    // }
    //
    // public Role(Providers.TMDB.Models.Shared.TmdbCast tmdbCast)
    // {
    //     Character = tmdbCast.Character;
    //     CreditId = tmdbCast.CreditId;
    //     Order = tmdbCast.Order;
    //     EpisodeCount = 0;
    // }
    //
    // public Role(Providers.TMDB.Models.Shared.TmdbGuestStar tmdbGuest)
    // {
    //     Character = tmdbGuest.CharacterName;
    //     CreditId = tmdbGuest.CreditId;
    //     Order = tmdbGuest.Order;
    //     EpisodeCount = 0;
    // }
}
