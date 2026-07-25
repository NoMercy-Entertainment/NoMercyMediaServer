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

using NoMercy.Database.Models.Playlists;

namespace NoMercy.Data.Repositories;

/// <summary>
/// User-created, ordered, VIDEO-ONLY playlists (movies + tv shows + episodes +
/// specials). Backed by its own <see cref="UserPlaylist"/> container
/// entity/table and attaches <see cref="PlaylistItem"/> children — entirely
/// separate from, and never touching, the music-only Playlist/PlaylistTrack
/// read path owned by MusicRepository.Playlists.cs. No table is shared between
/// the two features. Every method enforces playlist ownership
/// (playlist.UserId == the caller's userId) before reading or mutating
/// anything.
/// </summary>
public interface IUserPlaylistRepository
{
    /// <summary>Creates a new empty playlist owned by <paramref name="userId"/> and returns its id.</summary>
    Task<Guid> CreatePlaylistAsync(
        Guid userId,
        string name,
        string? description = null,
        string? cover = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Appends (or inserts at <paramref name="order"/>) one item to a playlist the
    /// caller owns. Returns null if the caller doesn't own the playlist, or the
    /// referenced media in <paramref name="item"/> doesn't exist.
    /// </summary>
    Task<PlaylistItem?> AddItemAsync(
        Guid playlistId,
        Guid userId,
        PlaylistItemRef item,
        int? order = null,
        CancellationToken ct = default
    );

    /// <summary>Removes one item from a playlist the caller owns. Returns false if not found/not owned.</summary>
    Task<bool> RemoveItemAsync(
        Guid playlistId,
        Guid userId,
        Ulid itemId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Re-assigns Order for every item in the playlist to match
    /// <paramref name="orderedItemIds"/>'s position. Rejects (returns false,
    /// no changes made) unless the caller owns the playlist and the id set
    /// supplied is exactly the playlist's current item set — a partial or
    /// stale list never silently drops or reorders a subset.
    /// </summary>
    Task<bool> ReorderAsync(
        Guid playlistId,
        Guid userId,
        IReadOnlyList<Ulid> orderedItemIds,
        CancellationToken ct = default
    );

    /// <summary>The caller's own playlists as lightweight summaries (id, name, cover, item count).</summary>
    Task<List<UserPlaylistSummary>> GetUserPlaylistsAsync(
        Guid userId,
        CancellationToken ct = default
    );

    /// <summary>
    /// The playlist's items in order, hydrated enough to render a card and start
    /// playback (title/poster/backdrop fields on the linked Movie/Tv/Episode/
    /// Special, plus video files for playable kinds). Returns null if the
    /// caller doesn't own the playlist (vs an empty list for an owned, empty one).
    /// </summary>
    Task<List<PlaylistItem>?> GetPlaylistItemsAsync(
        Guid playlistId,
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    );

    /// <summary>True if the playlist exists and is owned by <paramref name="userId"/>.</summary>
    Task<bool> OwnsPlaylistAsync(Guid playlistId, Guid userId, CancellationToken ct = default);

    /// <summary>The playlist's own metadata (no items). Returns null if not owned/found.</summary>
    Task<UserPlaylistDetail?> GetPlaylistAsync(
        Guid playlistId,
        Guid userId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Partially updates a playlist the caller owns — only non-null arguments are
    /// applied, so callers can PATCH a subset of Name/Description/Cover. Returns
    /// false if the caller doesn't own the playlist.
    /// </summary>
    Task<bool> UpdatePlaylistAsync(
        Guid playlistId,
        Guid userId,
        string? name,
        string? description,
        string? cover,
        CancellationToken ct = default
    );

    /// <summary>
    /// Deletes a playlist the caller owns. Its PlaylistItems cascade-delete via the
    /// FK_PlaylistItems_UserPlaylists_UserPlaylistId ON DELETE CASCADE constraint.
    /// Returns false if the caller doesn't own the playlist.
    /// </summary>
    Task<bool> DeletePlaylistAsync(Guid playlistId, Guid userId, CancellationToken ct = default);
}

/// <summary>The caller's own playlist, projected to just enough to render a list of cards.</summary>
public record UserPlaylistSummary(Guid Id, string Name, string? Cover, int ItemCount);

/// <summary>The caller's own playlist metadata, without items.</summary>
public record UserPlaylistDetail(Guid Id, string Name, string? Description, string? Cover);

/// <summary>
/// A single, type-safe reference to the one piece of media a new
/// <see cref="PlaylistItem"/> should point at. Constructed only through the
/// <c>For*</c> factories below, which makes "exactly one id set, matching
/// Kind" a structural guarantee instead of a runtime check callers can get
/// wrong.
/// </summary>
public readonly record struct PlaylistItemRef
{
    public PlaylistItemKind Kind { get; }
    public int? MovieId { get; }
    public int? TvId { get; }
    public int? EpisodeId { get; }
    public Ulid? SpecialId { get; }

    private PlaylistItemRef(
        PlaylistItemKind kind,
        int? movieId = null,
        int? tvId = null,
        int? episodeId = null,
        Ulid? specialId = null
    )
    {
        Kind = kind;
        MovieId = movieId;
        TvId = tvId;
        EpisodeId = episodeId;
        SpecialId = specialId;
    }

    public static PlaylistItemRef ForMovie(int movieId) =>
        new(PlaylistItemKind.Movie, movieId);

    public static PlaylistItemRef ForTv(int tvId) => new(PlaylistItemKind.Tv, tvId: tvId);

    public static PlaylistItemRef ForEpisode(int episodeId) =>
        new(PlaylistItemKind.Episode, episodeId: episodeId);

    public static PlaylistItemRef ForSpecial(Ulid specialId) =>
        new(PlaylistItemKind.Special, specialId: specialId);
}
