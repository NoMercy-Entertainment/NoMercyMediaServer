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
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Storage;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Unit")]
public class MusicRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly MusicRepository _repository;

    private static readonly Guid ArtistId1 = Guid.Parse(input: "a0000001-0000-0000-0000-000000000001");
    private static readonly Guid ArtistId2 = Guid.Parse(input: "a0000002-0000-0000-0000-000000000002");
    private static readonly Guid AlbumId1 = Guid.Parse(input: "b0000001-0000-0000-0000-000000000001");
    private static readonly Guid AlbumId2 = Guid.Parse(input: "b0000002-0000-0000-0000-000000000002");
    private static readonly Guid TrackId1 = Guid.Parse(input: "c0000001-0000-0000-0000-000000000001");
    private static readonly Guid TrackId2 = Guid.Parse(input: "c0000002-0000-0000-0000-000000000002");
    private static readonly Guid TrackId3 = Guid.Parse(input: "c0000003-0000-0000-0000-000000000003");

    public MusicRepositoryTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateFactory();
        _context = _factory.CreateDbContext();
        SeedMusicData(context: _context);
        _repository = new(contextFactory: _factory);
    }

    private static void SeedMusicData(MediaContext context)
    {
        // Phase 1: Base entities (no FK dependencies between each other)
        User testUser = new()
        {
            Id = SeedConstants.UserId,
            Email = "test@nomercy.tv",
            Name = "Test User",
            Owner = true,
            Allowed = true,
            Manage = true,
        };
        context.Users.Add(entity: testUser);

        Library musicLibrary = new()
        {
            Id = SeedConstants.MusicLibraryId,
            Title = "Music",
            Type = "music",
            Order = 3,
        };
        context.Libraries.Add(entity: musicLibrary);

        Driver systemLocalDriver = new()
        {
            Id = Driver.SystemLocalDriverId,
            Name = "Local Filesystem",
            Type = "local",
            Config = """{"rootPath":"/"}""",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(entity: systemLocalDriver);

        Folder musicFolder = new()
        {
            Id = SeedConstants.MusicFolderId,
            Path = "/media/music",
            DriverId = Driver.SystemLocalDriverId,
        };
        context.Folders.Add(entity: musicFolder);

        MusicGenre genre = new() { Id = Guid.NewGuid(), Name = "Rock" };
        context.MusicGenres.Add(entity: genre);

        context.SaveChanges();

        // Phase 2: Entities with FKs to phase 1
        context.LibraryUser.Add(entity: new(libraryId: SeedConstants.MusicLibraryId, userId: SeedConstants.UserId));
        context.FolderLibrary.Add(entity: new(folderId: SeedConstants.MusicFolderId, libraryId: SeedConstants.MusicLibraryId));

        Track track1 = new()
        {
            Id = TrackId1,
            Name = "Do I Wanna Know?",
            TrackNumber = 1,
            DiscNumber = 1,
            FolderId = SeedConstants.MusicFolderId,
            LibraryFolder = musicFolder,
        };
        Track track2 = new()
        {
            Id = TrackId2,
            Name = "R U Mine?",
            TrackNumber = 2,
            DiscNumber = 1,
            FolderId = SeedConstants.MusicFolderId,
            LibraryFolder = musicFolder,
        };
        Track track3 = new()
        {
            Id = TrackId3,
            Name = "Paranoid Android",
            TrackNumber = 1,
            DiscNumber = 1,
            FolderId = SeedConstants.MusicFolderId,
            LibraryFolder = musicFolder,
        };
        context.Tracks.AddRange(entities: [track1, track2, track3]);

        Artist artist1 = new()
        {
            Id = ArtistId1,
            Name = "Arctic Monkeys",
            Cover = "/arctic-monkeys.jpg",
            LibraryId = SeedConstants.MusicLibraryId,
            FolderId = SeedConstants.MusicFolderId,
            HostFolder = "/media/music/Arctic Monkeys",
            Library = musicLibrary,
            LibraryFolder = musicFolder,
        };
        Artist artist2 = new()
        {
            Id = ArtistId2,
            Name = "Radiohead",
            Cover = "/radiohead.jpg",
            LibraryId = SeedConstants.MusicLibraryId,
            FolderId = SeedConstants.MusicFolderId,
            HostFolder = "/media/music/Radiohead",
            Library = musicLibrary,
            LibraryFolder = musicFolder,
        };
        context.Artists.AddRange(entities: [artist1, artist2]);

        Album album1 = new()
        {
            Id = AlbumId1,
            Name = "AM",
            Cover = "/am.jpg",
            LibraryId = SeedConstants.MusicLibraryId,
            FolderId = SeedConstants.MusicFolderId,
            HostFolder = "/media/music/Arctic Monkeys/AM",
            Year = 2013,
            Library = musicLibrary,
            LibraryFolder = musicFolder,
        };
        Album album2 = new()
        {
            Id = AlbumId2,
            Name = "OK Computer",
            Cover = "/ok-computer.jpg",
            LibraryId = SeedConstants.MusicLibraryId,
            FolderId = SeedConstants.MusicFolderId,
            HostFolder = "/media/music/Radiohead/OK Computer",
            Year = 1997,
            Library = musicLibrary,
            LibraryFolder = musicFolder,
        };
        context.Albums.AddRange(entities: [album1, album2]);

        context.SaveChanges();

        // Phase 3: Join tables and play history
        context.AlbumTrack.AddRange(entities: [new AlbumTrack(albumId: AlbumId1, trackId: TrackId1), new AlbumTrack(albumId: AlbumId1, trackId: TrackId2), new AlbumTrack(albumId: AlbumId2, trackId: TrackId3)]
        );

        context.ArtistTrack.AddRange(entities: [new ArtistTrack(artistId: ArtistId1, trackId: TrackId1), new ArtistTrack(artistId: ArtistId1, trackId: TrackId2), new ArtistTrack(artistId: ArtistId2, trackId: TrackId3)]
        );

        context.AlbumArtist.AddRange(entities: [new AlbumArtist(albumId: AlbumId1, artistId: ArtistId1), new AlbumArtist(albumId: AlbumId2, artistId: ArtistId2)]
        );

        context.ArtistUser.Add(entity: new(artistId: ArtistId1, userId: SeedConstants.UserId));
        context.AlbumUser.Add(entity: new(albumId: AlbumId1, userId: SeedConstants.UserId));
        context.TrackUser.Add(entity: new(trackId: TrackId1, userId: SeedConstants.UserId));

        context.MusicPlays.AddRange(entities: [new MusicPlay(userId: SeedConstants.UserId, trackId: TrackId1), new MusicPlay(userId: SeedConstants.UserId, trackId: TrackId1), new MusicPlay(userId: SeedConstants.UserId, trackId: TrackId1), new MusicPlay(userId: SeedConstants.UserId, trackId: TrackId3)]
        );

        context.MusicGenreTrack.AddRange(entities: [new MusicGenreTrack(genreId: genre.Id, trackId: TrackId1), new MusicGenreTrack(genreId: genre.Id, trackId: TrackId2), new MusicGenreTrack(genreId: genre.Id, trackId: TrackId3)]
        );

        context.SaveChanges();
    }

    #region Browsable Query Tests

    [Fact]
    public async Task GetArtists_ReturnsList_ThatCanBePaginated()
    {
        List<Artist> result = (await _repository.GetArtists(userId: SeedConstants.UserId, letter: "A"))
            .Take(count: 1)
            .ToList();

        Assert.Single(collection: result);
        Assert.Equal(expected: "Arctic Monkeys", actual: result[index: 0].Name);
    }

    [Fact]
    public async Task GetArtists_ReturnsList_ThatCanBeFullyEnumerated()
    {
        List<Artist> result = await _repository.GetArtists(userId: SeedConstants.UserId, letter: "R");

        Assert.Single(collection: result);
        Assert.Equal(expected: "Radiohead", actual: result[index: 0].Name);
    }

    [Fact]
    public async Task GetAlbums_ReturnsList_ThatCanBePaginated()
    {
        List<Album> result = (await _repository.GetAlbums(userId: SeedConstants.UserId, letter: "A"))
            .Take(count: 1)
            .ToList();

        Assert.Single(collection: result);
        Assert.Equal(expected: "AM", actual: result[index: 0].Name);
    }

    [Fact]
    public async Task GetTracks_ReturnsList_ForUserFavorites()
    {
        List<TrackUser> result = await _repository.GetTracks(userId: SeedConstants.UserId);

        Assert.Single(collection: result);
        Assert.Equal(expected: TrackId1, actual: result[index: 0].TrackId);
    }

    [Fact]
    public async Task GetLatestAlbums_ReturnsList_ThatCanBePaginated()
    {
        List<Album> result = (await _repository.GetLatestAlbums()).Take(count: 1).ToList();

        Assert.Single(collection: result);
    }

    [Fact]
    public async Task GetLatestArtists_ReturnsList_ThatCanBePaginated()
    {
        List<Artist> result = (await _repository.GetLatestArtists()).Take(count: 1).ToList();

        Assert.Single(collection: result);
    }

    [Fact]
    public async Task GetLatestGenres_ReturnsList_OrderedByTrackCount()
    {
        List<MusicGenre> result = (await _repository.GetLatestGenres()).Take(count: 10).ToList();

        Assert.Single(collection: result);
        Assert.Equal(expected: "Rock", actual: result[index: 0].Name);
    }

    [Fact]
    public async Task GetFavoriteArtists_ReturnsList_ThatCanBePaginated()
    {
        List<ArtistUser> result = (await _repository.GetFavoriteArtists(userId: SeedConstants.UserId))
            .Take(count: 36)
            .ToList();

        Assert.Single(collection: result);
        Assert.Equal(expected: ArtistId1, actual: result[index: 0].ArtistId);
    }

    [Fact]
    public async Task GetFavoriteAlbums_ReturnsList_ThatCanBePaginated()
    {
        List<AlbumUser> result = (await _repository.GetFavoriteAlbums(userId: SeedConstants.UserId))
            .Take(count: 36)
            .ToList();

        Assert.Single(collection: result);
        Assert.Equal(expected: AlbumId1, actual: result[index: 0].AlbumId);
    }

    [Fact]
    public async Task GetFavoriteTracks_ReturnsList_ForUserFavorites()
    {
        List<TrackUser> result = await _repository.GetFavoriteTracks(userId: SeedConstants.UserId);

        Assert.Single(collection: result);
        Assert.Equal(expected: TrackId1, actual: result[index: 0].TrackId);
    }

    #endregion

    #region Terminal Query Tests

    [Fact]
    public async Task GetFavoriteArtistAsync_ReturnsMaterializedList()
    {
        List<ArtistTrack> result = await _repository.GetFavoriteArtistAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: result);
        Assert.Contains(collection: result, filter: at => at.ArtistId == ArtistId1);
    }

    [Fact]
    public async Task GetFavoriteArtistAsync_CanBeGroupedClientSide()
    {
        List<ArtistTrack> result = await _repository.GetFavoriteArtistAsync(userId: SeedConstants.UserId);

        IGrouping<Guid, ArtistTrack>? topArtist = result
            .GroupBy(keySelector: at => at.ArtistId)
            .MaxBy(keySelector: g => g.Count());

        Assert.NotNull(@object: topArtist);
        Assert.Equal(expected: ArtistId1, actual: topArtist.Key);
    }

    [Fact]
    public async Task GetFavoriteAlbumAsync_ReturnsMaterializedList()
    {
        List<AlbumTrack> result = await _repository.GetFavoriteAlbumAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: result);
        Assert.Contains(collection: result, filter: at => at.AlbumId == AlbumId1);
    }

    [Fact]
    public async Task GetFavoritePlaylistAsync_ReturnsMaterializedList()
    {
        List<PlaylistTrack> result = await _repository.GetFavoritePlaylistAsync(
            userId: SeedConstants.UserId
        );

        Assert.NotNull(@object: result);
    }

    [Fact]
    public async Task GetFavoriteArtistAsync_ReturnsEmptyForUnknownUser()
    {
        List<ArtistTrack> result = await _repository.GetFavoriteArtistAsync(
            userId: SeedConstants.OtherUserId
        );

        Assert.Empty(collection: result);
    }

    [Fact]
    public async Task GetFavoriteAlbumAsync_ReturnsEmptyForUnknownUser()
    {
        List<AlbumTrack> result = await _repository.GetFavoriteAlbumAsync(
            userId: SeedConstants.OtherUserId
        );

        Assert.Empty(collection: result);
    }

    #endregion

    #region No Disposed Context Tests

    [Fact]
    public async Task BrowsableQueries_DoNotThrowDisposedContextException()
    {
        List<Artist> artists = await _repository.GetArtists(userId: SeedConstants.UserId, letter: "_");
        List<Album> albums = await _repository.GetAlbums(userId: SeedConstants.UserId, letter: "_");
        List<TrackUser> tracks = await _repository.GetTracks(userId: SeedConstants.UserId);

        Assert.NotNull(@object: artists);
        Assert.NotNull(@object: albums);
        Assert.NotNull(@object: tracks);
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
