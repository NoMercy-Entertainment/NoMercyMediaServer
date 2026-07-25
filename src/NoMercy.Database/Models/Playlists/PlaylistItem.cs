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

namespace NoMercy.Database.Models.Playlists;

/// <summary>
/// Discriminates which of PlaylistItem's polymorphic FKs is populated. Kept as
/// an enum (stored as INTEGER) rather than a string so a bad value can never be
/// typed in — the compiler enforces the closed set. Deliberately VIDEO-ONLY —
/// there is no Track kind. A user playlist routes every item to the video
/// player; music has its own separate playlist feature (see
/// <see cref="Music.Playlist"/>/<see cref="Music.PlaylistTrack"/>) that this
/// enum never references.
/// </summary>
public enum PlaylistItemKind
{
    Movie,
    Tv,
    Episode,
    Special,
}

/// <summary>
/// One entry in a user-created, ordered, VIDEO-ONLY playlist. FKs to
/// <see cref="Playlists.UserPlaylist"/> — its own container entity/table,
/// entirely separate from the music-only <see cref="Music.Playlist"/>/
/// <see cref="Music.PlaylistTrack"/> tables and their read path. No shared
/// table exists between the two features, so neither can leak into the
/// other's query results. Every item references exactly one of Movie / Tv /
/// Episode / Special, selected by <see cref="Kind"/>. Application code (not a
/// DB CHECK constraint, matching the existing ContentSegment/SpecialItem
/// convention in this schema) enforces that only the FK matching
/// <see cref="Kind"/> is set.
///
/// No inverse collection navigation is added to UserPlaylist/Movie/Tv/Episode/
/// Special — read paths root directly at PlaylistItem instead, which avoids
/// the AsNoTracking Include-cycle trap those entities would otherwise create
/// for callers rooted at PlaylistItem.
/// </summary>
[PrimaryKey(nameof(Id))]
[Index(nameof(UserPlaylistId), nameof(Order))]
public class PlaylistItem : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("user_playlist_id")]
    public Guid UserPlaylistId { get; set; }
    public UserPlaylist UserPlaylist { get; set; } = null!;

    [JsonProperty("kind")]
    public PlaylistItemKind Kind { get; set; }

    /// <summary>Stable user-defined sort key within the playlist. Not required to be gap-free.</summary>
    [JsonProperty("order")]
    public int Order { get; set; }

    [JsonProperty("movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty("tv_id")]
    public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty("episode_id")]
    public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    [JsonProperty("special_id")]
    public Ulid? SpecialId { get; set; }
    public Special? Special { get; set; }
}
