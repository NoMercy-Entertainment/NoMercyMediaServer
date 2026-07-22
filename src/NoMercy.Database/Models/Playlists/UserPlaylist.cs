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

namespace NoMercy.Database.Models.Playlists;

/// <summary>
/// A user-created, ordered, VIDEO-ONLY playlist (movies, tv shows, episodes and
/// specials). This is its own container entity/table — deliberately NOT the
/// music-only <see cref="Music.Playlist"/> table (which stays reserved for
/// PlaylistTrack), NOT <see cref="Movies.Collection"/> (TMDB franchise
/// groupings) and NOT <see cref="TvShows.Special"/> (admin-curated). Owns a
/// set of <see cref="PlaylistItem"/> rows via <c>PlaylistItem.UserPlaylistId</c>;
/// there is no shared table with the music playlist feature, so neither can
/// appear in the other's query results.
/// </summary>
[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(UserId))]
public class UserPlaylist : Timestamps
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

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }

    [JsonProperty(propertyName: "user")]
    public User User { get; set; } = null!;
}
