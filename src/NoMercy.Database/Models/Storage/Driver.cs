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

namespace NoMercy.Database.Models.Storage;

[PrimaryKey(nameof(Id))]
[Index(nameof(Name), IsUnique = true)]
[Index(nameof(Type))]
public class Driver
{
    // Stable sentinel for the built-in local-filesystem driver that is
    // auto-seeded on first boot. Hardcoded so every install uses the same
    // Ulid — clients can rely on it without querying the DB.
    public static readonly Ulid SystemLocalDriverId = Ulid.Parse("01JKQSTS00000000000000000A");

    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [Required]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("config")]
    public string? Config { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("folders")]
    public ICollection<Folder> Folders { get; set; } = [];
}
