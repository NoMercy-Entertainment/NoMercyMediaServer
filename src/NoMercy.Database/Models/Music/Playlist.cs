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

namespace NoMercy.Database.Models.Music;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(UserId))]
public class Playlist : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "filename")]
    public string? Filename { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string? Duration { get; set; }

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }

    [JsonProperty(propertyName: "user")]
    public User User { get; set; } = null!;

    [JsonProperty(propertyName: "playlist_track")]
    public ICollection<PlaylistTrack> Tracks { get; set; } = [];
}
