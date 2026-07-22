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

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(CreditId), IsUnique = true)]
public class Job
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "task")]
    public string? Task { get; set; }

    [JsonProperty(propertyName: "episode_count")]
    public int? EpisodeCount { get; set; }

    [JsonProperty(propertyName: "order")]
    public int? Order { get; set; }

    [JsonProperty(propertyName: "credit_id")]
    public string CreditId { get; set; } = null!;
    public Crew? Crew { get; set; } = null!;
}
