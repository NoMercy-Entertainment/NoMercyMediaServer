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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Playlists;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// Coverage for the user-created, VIDEO-ONLY playlist slice: the UserPlaylist
/// container entity + PlaylistItem schema + UserPlaylistRepository. Backed by
/// its own UserPlaylists table — entirely separate from, and never touching,
/// the music-only Playlist/PlaylistTrack tables/read path owned by
/// MusicRepository.Playlists.cs. <see cref="ExistingMusicPlaylist_PlaylistTrackPath_StillWorks"/>
/// proves both directions of that separation: the legacy music playlist round
/// trips correctly through MusicRepository, and it never surfaces through
/// UserPlaylistRepository.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class UserPlaylistRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly UserPlaylistRepository _repository;

    private static readonly Guid OtherUserId = Guid.Parse(input: "e0000001-0000-0000-0000-000000000002");

    public UserPlaylistRepositoryTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();
        _context = _factory.CreateDbContext();
        _repository = new(contextFactory: _factory);

        _context.Users.Add(
            entity: new()
            {
                Id = OtherUserId,
                Email = "other@nomercy.tv",
                Name = "Other User",
                Owner = false,
                Allowed = true,
                Manage = false,
            }
        );
        _context.SaveChanges();
    }

    private Track SeedTrack(string name = "Test Track")
    {
        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            TrackNumber = 1,
            DiscNumber = 1,
            FolderId = SeedConstants.MovieFolderId,
        };
        _context.Tracks.Add(entity: track);
        _context.SaveChanges();
        return track;
    }

    private Special SeedSpecial(string title = "Test Special")
    {
        Special special = new() { Id = Ulid.NewUlid(), Title = title };
        _context.Specials.Add(entity: special);
        _context.SaveChanges();
        return special;
    }

    [Fact]
    public async Task CreatePlaylistAsync_PersistsPlaylist_OwnedByCaller()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(
            userId: SeedConstants.UserId,
            name: "My Video Playlist",
            description: "a description",
            cover: "/cover.jpg"
        );

        UserPlaylist? saved = await _context.UserPlaylists.FirstOrDefaultAsync(predicate: p =>
            p.Id == playlistId
        );
        Assert.NotNull(@object: saved);
        Assert.Equal(expected: "My Video Playlist", actual: saved!.Name);
        Assert.Equal(expected: "a description", actual: saved.Description);
        Assert.Equal(expected: "/cover.jpg", actual: saved.Cover);
        Assert.Equal(expected: SeedConstants.UserId, actual: saved.UserId);
    }

    [Fact]
    public async Task AddItemAsync_MovieEpisodeAndSpecial_AreReturnedInOrder_WithCorrectKind()
    {
        Special special = SeedSpecial();

        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");

        PlaylistItem? movieItem = await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForMovie(movieId: 129)
        );
        PlaylistItem? episodeItem = await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForEpisode(episodeId: 62085)
        );
        PlaylistItem? specialItem = await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForSpecial(specialId: special.Id)
        );

        Assert.NotNull(@object: movieItem);
        Assert.NotNull(@object: episodeItem);
        Assert.NotNull(@object: specialItem);
        Assert.Equal(expected: 0, actual: movieItem!.Order);
        Assert.Equal(expected: 1, actual: episodeItem!.Order);
        Assert.Equal(expected: 2, actual: specialItem!.Order);

        List<PlaylistItem>? items = await _repository.GetPlaylistItemsAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            language: "en",
            country: "US"
        );

        Assert.NotNull(@object: items);
        Assert.Equal(expected: 3, actual: items!.Count);

        Assert.Equal(expected: PlaylistItemKind.Movie, actual: items[index: 0].Kind);
        Assert.Equal(expected: 129, actual: items[index: 0].MovieId);
        Assert.Equal(expected: "Spirited Away", actual: items[index: 0].Movie?.Title);

        Assert.Equal(expected: PlaylistItemKind.Episode, actual: items[index: 1].Kind);
        Assert.Equal(expected: 62085, actual: items[index: 1].EpisodeId);
        Assert.Equal(expected: "Pilot", actual: items[index: 1].Episode?.Title);
        Assert.Equal(expected: 1399, actual: items[index: 1].Episode?.Tv.Id);

        Assert.Equal(expected: PlaylistItemKind.Special, actual: items[index: 2].Kind);
        Assert.Equal(expected: special.Id, actual: items[index: 2].SpecialId);
        Assert.Equal(expected: "Test Special", actual: items[index: 2].Special?.Title);
    }

    [Fact]
    public async Task AddItemAsync_WithExplicitOrder_InsertsAndShiftsExistingItems()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");

        PlaylistItem? first = await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForMovie(movieId: 129)
        );
        PlaylistItem? second = await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForMovie(movieId: 680)
        );

        // Insert a third movie at the front — 129 and 680 should both shift down.
        Special special = SeedSpecial();
        PlaylistItem? inserted = await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForSpecial(specialId: special.Id),
            order: 0
        );

        Assert.NotNull(@object: inserted);
        Assert.Equal(expected: 0, actual: inserted!.Order);

        List<PlaylistItem>? items = await _repository.GetPlaylistItemsAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            language: "en",
            country: "US"
        );

        Assert.NotNull(@object: items);
        Assert.Equal(expected: 3, actual: items!.Count);
        Assert.Equal(expected: PlaylistItemKind.Special, actual: items[index: 0].Kind);
        Assert.Equal(expected: special.Id, actual: items[index: 0].SpecialId);
        Assert.Equal(expected: 129, actual: items[index: 1].MovieId);
        Assert.Equal(expected: 1, actual: items[index: 1].Order);
        Assert.Equal(expected: 680, actual: items[index: 2].MovieId);
        Assert.Equal(expected: 2, actual: items[index: 2].Order);
    }

    [Fact]
    public async Task AddItemAsync_RejectsNonexistentMedia()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");

        PlaylistItem? result = await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForMovie(movieId: 999_999)
        );

        Assert.Null(@object: result);
        Assert.Empty(
            collection: await _context.PlaylistItems.Where(predicate: pi => pi.UserPlaylistId == playlistId).ToListAsync()
        );
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesOwnedItem()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");
        PlaylistItem item = (
            await _repository.AddItemAsync(
                playlistId: playlistId,
                userId: SeedConstants.UserId,
                item: PlaylistItemRef.ForMovie(movieId: 129)
            )
        )!;

        bool removed = await _repository.RemoveItemAsync(playlistId: playlistId, userId: SeedConstants.UserId, itemId: item.Id);

        Assert.True(condition: removed);
        Assert.Null(@object: await _context.PlaylistItems.FirstOrDefaultAsync(predicate: pi => pi.Id == item.Id));
    }

    [Fact]
    public async Task ReorderAsync_AppliesRequestedOrder()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");
        PlaylistItem a = (
            await _repository.AddItemAsync(
                playlistId: playlistId,
                userId: SeedConstants.UserId,
                item: PlaylistItemRef.ForMovie(movieId: 129)
            )
        )!;
        PlaylistItem b = (
            await _repository.AddItemAsync(
                playlistId: playlistId,
                userId: SeedConstants.UserId,
                item: PlaylistItemRef.ForMovie(movieId: 680)
            )
        )!;

        bool reordered = await _repository.ReorderAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            orderedItemIds: [b.Id, a.Id]
        );

        Assert.True(condition: reordered);

        List<PlaylistItem>? items = await _repository.GetPlaylistItemsAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            language: "en",
            country: "US"
        );
        Assert.NotNull(@object: items);
        Assert.Equal(expected: b.Id, actual: items![index: 0].Id);
        Assert.Equal(expected: a.Id, actual: items[index: 1].Id);
    }

    [Fact]
    public async Task ReorderAsync_RejectsPartialItemList()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");
        PlaylistItem a = (
            await _repository.AddItemAsync(
                playlistId: playlistId,
                userId: SeedConstants.UserId,
                item: PlaylistItemRef.ForMovie(movieId: 129)
            )
        )!;
        await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForMovie(movieId: 680)
        );

        // Omits the second item entirely — must be rejected, not silently applied.
        bool reordered = await _repository.ReorderAsync(playlistId: playlistId, userId: SeedConstants.UserId, orderedItemIds: [a.Id]);

        Assert.False(condition: reordered);
    }

    [Fact]
    public async Task GetUserPlaylistsAsync_ReturnsOwnPlaylists_WithItemCount()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(
            userId: SeedConstants.UserId,
            name: "Mix",
            cover: "/cover.jpg"
        );
        await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForMovie(movieId: 129)
        );
        await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForMovie(movieId: 680)
        );

        List<UserPlaylistSummary> playlists = await _repository.GetUserPlaylistsAsync(
            userId: SeedConstants.UserId
        );

        UserPlaylistSummary summary = Assert.Single(collection: playlists);
        Assert.Equal(expected: playlistId, actual: summary.Id);
        Assert.Equal(expected: "Mix", actual: summary.Name);
        Assert.Equal(expected: "/cover.jpg", actual: summary.Cover);
        Assert.Equal(expected: 2, actual: summary.ItemCount);
    }

    #region Ownership isolation

    [Fact]
    public async Task GetPlaylistItemsAsync_ReturnsNull_WhenCallerDoesNotOwnPlaylist()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");
        await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            item: PlaylistItemRef.ForMovie(movieId: 129)
        );

        List<PlaylistItem>? items = await _repository.GetPlaylistItemsAsync(
            playlistId: playlistId,
            userId: OtherUserId,
            language: "en",
            country: "US"
        );

        Assert.Null(@object: items);
    }

    [Fact]
    public async Task AddItemAsync_ReturnsNull_WhenCallerDoesNotOwnPlaylist()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");

        PlaylistItem? result = await _repository.AddItemAsync(
            playlistId: playlistId,
            userId: OtherUserId,
            item: PlaylistItemRef.ForMovie(movieId: 129)
        );

        Assert.Null(@object: result);
    }

    [Fact]
    public async Task RemoveItemAsync_ReturnsFalse_WhenCallerDoesNotOwnPlaylist()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");
        PlaylistItem item = (
            await _repository.AddItemAsync(
                playlistId: playlistId,
                userId: SeedConstants.UserId,
                item: PlaylistItemRef.ForMovie(movieId: 129)
            )
        )!;

        bool removed = await _repository.RemoveItemAsync(playlistId: playlistId, userId: OtherUserId, itemId: item.Id);

        Assert.False(condition: removed);
        Assert.NotNull(@object: await _context.PlaylistItems.FirstOrDefaultAsync(predicate: pi => pi.Id == item.Id));
    }

    [Fact]
    public async Task ReorderAsync_ReturnsFalse_WhenCallerDoesNotOwnPlaylist()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");
        PlaylistItem a = (
            await _repository.AddItemAsync(
                playlistId: playlistId,
                userId: SeedConstants.UserId,
                item: PlaylistItemRef.ForMovie(movieId: 129)
            )
        )!;

        bool reordered = await _repository.ReorderAsync(playlistId: playlistId, userId: OtherUserId, orderedItemIds: [a.Id]);

        Assert.False(condition: reordered);
    }

    [Fact]
    public async Task GetUserPlaylistsAsync_DoesNotReturnOtherUsersPlaylists()
    {
        await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mine");

        List<UserPlaylistSummary> otherUsersPlaylists = await _repository.GetUserPlaylistsAsync(
            userId: OtherUserId
        );

        Assert.Empty(collection: otherUsersPlaylists);
    }

    #endregion

    #region Playlist metadata — Owns/Get/Update/Delete

    [Fact]
    public async Task OwnsPlaylistAsync_ReturnsTrue_ForOwner_False_ForOtherUserOrUnknownId()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");

        Assert.True(condition: await _repository.OwnsPlaylistAsync(playlistId: playlistId, userId: SeedConstants.UserId));
        Assert.False(condition: await _repository.OwnsPlaylistAsync(playlistId: playlistId, userId: OtherUserId));
        Assert.False(condition: await _repository.OwnsPlaylistAsync(playlistId: Guid.NewGuid(), userId: SeedConstants.UserId));
    }

    [Fact]
    public async Task GetPlaylistAsync_ReturnsMetadata_ForOwner_Null_ForOtherUser()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(
            userId: SeedConstants.UserId,
            name: "Mix",
            description: "a description",
            cover: "/cover.jpg"
        );

        UserPlaylistDetail? detail = await _repository.GetPlaylistAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId
        );

        Assert.NotNull(@object: detail);
        Assert.Equal(expected: playlistId, actual: detail!.Id);
        Assert.Equal(expected: "Mix", actual: detail.Name);
        Assert.Equal(expected: "a description", actual: detail.Description);
        Assert.Equal(expected: "/cover.jpg", actual: detail.Cover);

        Assert.Null(@object: await _repository.GetPlaylistAsync(playlistId: playlistId, userId: OtherUserId));
    }

    [Fact]
    public async Task UpdatePlaylistAsync_AppliesOnlyProvidedFields()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(
            userId: SeedConstants.UserId,
            name: "Original Name",
            description: "Original description",
            cover: "/original.jpg"
        );

        // Only Name is provided — Description and Cover must be left untouched.
        bool updated = await _repository.UpdatePlaylistAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId,
            name: "Renamed",
            description: null,
            cover: null
        );

        Assert.True(condition: updated);

        UserPlaylistDetail? detail = await _repository.GetPlaylistAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId
        );
        Assert.NotNull(@object: detail);
        Assert.Equal(expected: "Renamed", actual: detail!.Name);
        Assert.Equal(expected: "Original description", actual: detail.Description);
        Assert.Equal(expected: "/original.jpg", actual: detail.Cover);
    }

    [Fact]
    public async Task UpdatePlaylistAsync_ReturnsFalse_WhenCallerDoesNotOwnPlaylist()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");

        bool updated = await _repository.UpdatePlaylistAsync(
            playlistId: playlistId,
            userId: OtherUserId,
            name: "Hijacked",
            description: null,
            cover: null
        );

        Assert.False(condition: updated);

        UserPlaylistDetail? detail = await _repository.GetPlaylistAsync(
            playlistId: playlistId,
            userId: SeedConstants.UserId
        );
        Assert.Equal(expected: "Mix", actual: detail!.Name);
    }

    [Fact]
    public async Task DeletePlaylistAsync_RemovesPlaylistAndCascadesItems()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");
        PlaylistItem item = (
            await _repository.AddItemAsync(
                playlistId: playlistId,
                userId: SeedConstants.UserId,
                item: PlaylistItemRef.ForMovie(movieId: 129)
            )
        )!;

        bool deleted = await _repository.DeletePlaylistAsync(playlistId: playlistId, userId: SeedConstants.UserId);

        Assert.True(condition: deleted);
        Assert.Null(@object: await _context.UserPlaylists.FirstOrDefaultAsync(predicate: p => p.Id == playlistId));
        Assert.Null(@object: await _context.PlaylistItems.FirstOrDefaultAsync(predicate: pi => pi.Id == item.Id));
    }

    [Fact]
    public async Task DeletePlaylistAsync_ReturnsFalse_WhenCallerDoesNotOwnPlaylist()
    {
        Guid playlistId = await _repository.CreatePlaylistAsync(userId: SeedConstants.UserId, name: "Mix");

        bool deleted = await _repository.DeletePlaylistAsync(playlistId: playlistId, userId: OtherUserId);

        Assert.False(condition: deleted);
        Assert.NotNull(@object: await _context.UserPlaylists.FirstOrDefaultAsync(predicate: p => p.Id == playlistId));
    }

    #endregion

    /// <summary>
    /// Proves the video-only UserPlaylist/PlaylistItem schema shares zero tables
    /// with the existing music-only Playlist/PlaylistTrack read path: a legacy
    /// music playlist created and read purely through PlaylistTrack still
    /// round-trips correctly via MusicRepository (direction one), and it never
    /// surfaces through UserPlaylistRepository — the video-only endpoint's own
    /// user (direction two, the leak the original implementation had).
    /// </summary>
    [Fact]
    public async Task ExistingMusicPlaylist_PlaylistTrackPath_StillWorks()
    {
        Track track = SeedTrack(name: "Legacy Track");

        Playlist legacyPlaylist = new()
        {
            Id = Guid.NewGuid(),
            Name = "Legacy Music Playlist",
            UserId = SeedConstants.UserId,
        };
        _context.Playlists.Add(entity: legacyPlaylist);
        _context.SaveChanges();

        _context.PlaylistTrack.Add(entity: new(playlistId: legacyPlaylist.Id, trackId: track.Id));
        _context.SaveChanges();

        MusicRepository musicRepository = new(contextFactory: _factory);
        List<PlaylistTrack> tracks = await musicRepository.GetPlaylistTracksAsync(
            userId: SeedConstants.UserId,
            playlistId: legacyPlaylist.Id
        );

        Assert.Single(collection: tracks);
        Assert.Equal(expected: "Legacy Track", actual: tracks[index: 0].Track.Name);

        // Direction two: the same user's video-only playlist list is empty — the
        // music playlist never appears there, because the two features share no
        // table (UserPlaylists is a distinct table from Playlists, and there is
        // no UserPlaylist row with the music playlist's id).
        Assert.Empty(collection: await _repository.GetUserPlaylistsAsync(userId: SeedConstants.UserId));
        Assert.False(condition: await _context.UserPlaylists.AnyAsync(predicate: p => p.Id == legacyPlaylist.Id));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(obj: this);
    }
}
