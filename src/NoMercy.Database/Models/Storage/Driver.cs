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

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Name), IsUnique = true)]
[Index(propertyName: nameof(Type))]
public class Driver
{
    // Stable sentinel for the built-in local-filesystem driver that is
    // auto-seeded on first boot. Hardcoded so every install uses the same
    // Ulid — clients can rely on it without querying the DB.
    public static readonly Ulid SystemLocalDriverId = Ulid.Parse(base32: "01JKQSTS00000000000000000A");

    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [Required]
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "config")]
    public string? Config { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty(propertyName: "folders")]
    public ICollection<Folder> Folders { get; set; } = [];
}
