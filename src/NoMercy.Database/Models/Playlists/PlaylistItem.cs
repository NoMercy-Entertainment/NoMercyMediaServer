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
/// typed in — the compiler enforces the closed set.
/// </summary>
public enum PlaylistItemKind
{
    Movie,
    Tv,
    Episode,
    Track,
    Special,
}

/// <summary>
/// One entry in a user-created, ordered, mixed-media playlist (task 10). This is
/// deliberately a NEW, additive entity — it does not touch the existing
/// music-only <see cref="Playlist"/>/<see cref="PlaylistTrack"/> tables or their
/// read paths. It reuses <see cref="Playlist"/> as the container (Name /
/// Description / Cover / UserId already live there) and attaches ordered items
/// that reference exactly one of Movie / Tv / Episode / Track / Special,
/// selected by <see cref="Kind"/>. Application code (not a DB CHECK constraint,
/// matching the existing ContentSegment/SpecialItem convention in this schema)
/// enforces that only the FK matching <see cref="Kind"/> is set.
///
/// No inverse collection navigation is added to Playlist/Movie/Tv/Episode/Track/
/// Special — this keeps the change fully additive (none of those files are
/// touched) and avoids the AsNoTracking Include-cycle trap those entities would
/// otherwise create for callers rooted at PlaylistItem.
/// </summary>
[PrimaryKey(nameof(Id))]
[Index(nameof(PlaylistId), nameof(Order))]
public class PlaylistItem : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("playlist_id")]
    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

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

    [JsonProperty("track_id")]
    public Guid? TrackId { get; set; }
    public Track? Track { get; set; }

    [JsonProperty("special_id")]
    public Ulid? SpecialId { get; set; }
    public Special? Special { get; set; }
}
