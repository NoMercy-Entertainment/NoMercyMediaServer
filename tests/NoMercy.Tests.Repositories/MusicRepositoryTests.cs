using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Unit")]
public class MusicRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly MusicRepository _repository;

    private static readonly Guid ArtistId1 = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid ArtistId2 = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid AlbumId1 = Guid.Parse("b0000001-0000-0000-0000-000000000001");
    private static readonly Guid AlbumId2 = Guid.Parse("b0000002-0000-0000-0000-000000000002");
    private static readonly Guid TrackId1 = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid TrackId2 = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid TrackId3 = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    public MusicRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateContext();
        SeedMusicData(_context);
        _repository = new(_context);
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
            Manage = true
        };
        context.Users.Add(testUser);

        Library musicLibrary = new()
        {
            Id = SeedConstants.MusicLibraryId,
            Title = "Music",
            Type = "music",
            Order = 3
        };
        context.Libraries.Add(musicLibrary);

        Folder musicFolder = new()
        {
            Id = SeedConstants.MusicFolderId,
            Path = "/media/music"
        };
        context.Folders.Add(musicFolder);

        MusicGenre genre = new() { Id = Guid.NewGuid(), Name = "Rock" };
        context.MusicGenres.Add(genre);

        context.SaveChanges();

        // Phase 2: Entities with FKs to phase 1
        context.LibraryUser.Add(new(SeedConstants.MusicLibraryId, SeedConstants.UserId));
        context.FolderLibrary.Add(new(SeedConstants.MusicFolderId, SeedConstants.MusicLibraryId));

        Track track1 = new()
        {
            Id = TrackId1,
            Name = "Do I Wanna Know?",
            TrackNumber = 1,
            DiscNumber = 1,
            FolderId = SeedConstants.MusicFolderId,
            LibraryFolder = musicFolder
        };
        Track track2 = new()
        {
            Id = TrackId2,
            Name = "R U Mine?",
            TrackNumber = 2,
            DiscNumber = 1,
            FolderId = SeedConstants.MusicFolderId,
            LibraryFolder = musicFolder
        };
        Track track3 = new()
        {
            Id = TrackId3,
            Name = "Paranoid Android",
            TrackNumber = 1,
            DiscNumber = 1,
            FolderId = SeedConstants.MusicFolderId,
            LibraryFolder = musicFolder
        };
        context.Tracks.AddRange(track1, track2, track3);

        Artist artist1 = new()
        {
            Id = ArtistId1,
            Name = "Arctic Monkeys",
            Cover = "/arctic-monkeys.jpg",
            LibraryId = SeedConstants.MusicLibraryId,
            FolderId = SeedConstants.MusicFolderId,
            HostFolder = "/media/music/Arctic Monkeys",
            Library = musicLibrary,
            LibraryFolder = musicFolder
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
            LibraryFolder = musicFolder
        };
        context.Artists.AddRange(artist1, artist2);

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
            LibraryFolder = musicFolder
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
            LibraryFolder = musicFolder
        };
        context.Albums.AddRange(album1, album2);

        context.SaveChanges();

        // Phase 3: Join tables and play history
        context.AlbumTrack.AddRange(
            new AlbumTrack(AlbumId1, TrackId1),
            new AlbumTrack(AlbumId1, TrackId2),
            new AlbumTrack(AlbumId2, TrackId3));

        context.ArtistTrack.AddRange(
            new ArtistTrack(ArtistId1, TrackId1),
            new ArtistTrack(ArtistId1, TrackId2),
            new ArtistTrack(ArtistId2, TrackId3));

        context.AlbumArtist.AddRange(
            new AlbumArtist(AlbumId1, ArtistId1),
            new AlbumArtist(AlbumId2, ArtistId2));

        context.ArtistUser.Add(new(ArtistId1, SeedConstants.UserId));
        context.AlbumUser.Add(new(AlbumId1, SeedConstants.UserId));
        context.TrackUser.Add(new(TrackId1, SeedConstants.UserId));

        context.MusicPlays.AddRange(
            new MusicPlay(SeedConstants.UserId, TrackId1),
            new MusicPlay(SeedConstants.UserId, TrackId1),
            new MusicPlay(SeedConstants.UserId, TrackId1),
            new MusicPlay(SeedConstants.UserId, TrackId3));

        context.MusicGenreTrack.AddRange(
            new MusicGenreTrack(genre.Id, TrackId1),
            new MusicGenreTrack(genre.Id, TrackId2),
            new MusicGenreTrack(genre.Id, TrackId3));

        context.SaveChanges();
    }

    #region Browsable Query Tests

    [Fact]
    public void GetArtists_ReturnsIQueryable_ThatCanBePaginated()
    {
        IQueryable<Artist> query = _repository.GetArtists(SeedConstants.UserId, "A");

        List<Artist> result = query.Take(1).ToList();

        Assert.Single(result);
        Assert.Equal("Arctic Monkeys", result[0].Name);
    }

    [Fact]
    public void GetArtists_ReturnsIQueryable_ThatCanBeFullyEnumerated()
    {
        IQueryable<Artist> query = _repository.GetArtists(SeedConstants.UserId, "R");

        List<Artist> result = query.ToList();

        Assert.Single(result);
        Assert.Equal("Radiohead", result[0].Name);
    }

    [Fact]
    public void GetAlbums_ReturnsIQueryable_ThatCanBePaginated()
    {
        IQueryable<Album> query = _repository.GetAlbums(SeedConstants.UserId, "A");

        List<Album> result = query.Take(1).ToList();

        Assert.Single(result);
        Assert.Equal("AM", result[0].Name);
    }

    [Fact]
    public void GetTracks_ReturnsIQueryable_ForUserFavorites()
    {
        IQueryable<TrackUser> query = _repository.GetTracks(SeedConstants.UserId);

        List<TrackUser> result = query.ToList();

        Assert.Single(result);
        Assert.Equal(TrackId1, result[0].TrackId);
    }

    [Fact]
    public async Task GetLatestAlbums_ReturnsIQueryable_ThatCanBePaginatedAsync()
    {
        List<Album> result = await _repository.GetLatestAlbums()
            .Take(1)
            .ToListAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetLatestArtists_ReturnsIQueryable_ThatCanBePaginatedAsync()
    {
        List<Artist> result = await _repository.GetLatestArtists()
            .Take(1)
            .ToListAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetLatestGenres_ReturnsIQueryable_OrderedByTrackCount()
    {
        List<MusicGenre> result = await _repository.GetLatestGenres()
            .Take(10)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Rock", result[0].Name);
    }

    [Fact]
    public async Task GetFavoriteArtists_ReturnsIQueryable_ThatCanBePaginated()
    {
        List<ArtistUser> result = await _repository.GetFavoriteArtists(SeedConstants.UserId)
            .Take(36)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal(ArtistId1, result[0].ArtistId);
    }

    [Fact]
    public async Task GetFavoriteAlbums_ReturnsIQueryable_ThatCanBePaginated()
    {
        List<AlbumUser> result = await _repository.GetFavoriteAlbums(SeedConstants.UserId)
            .Take(36)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal(AlbumId1, result[0].AlbumId);
    }

    [Fact]
    public void GetFavoriteTracks_ReturnsIQueryable_ForUserFavorites()
    {
        IQueryable<TrackUser> query = _repository.GetFavoriteTracks(SeedConstants.UserId);

        List<TrackUser> result = query.ToList();

        Assert.Single(result);
        Assert.Equal(TrackId1, result[0].TrackId);
    }

    #endregion

    #region Terminal Query Tests

    [Fact]
    public async Task GetFavoriteArtistAsync_ReturnsMaterializedList()
    {
        List<ArtistTrack> result = await _repository.GetFavoriteArtistAsync(SeedConstants.UserId);

        Assert.NotEmpty(result);
        Assert.Contains(result, at => at.ArtistId == ArtistId1);
    }

    [Fact]
    public async Task GetFavoriteArtistAsync_CanBeGroupedClientSide()
    {
        List<ArtistTrack> result = await _repository.GetFavoriteArtistAsync(SeedConstants.UserId);

        IGrouping<Guid, ArtistTrack>? topArtist = result
            .GroupBy(at => at.ArtistId)
            .MaxBy(g => g.Count());

        Assert.NotNull(topArtist);
        Assert.Equal(ArtistId1, topArtist.Key);
    }

    [Fact]
    public async Task GetFavoriteAlbumAsync_ReturnsMaterializedList()
    {
        List<AlbumTrack> result = await _repository.GetFavoriteAlbumAsync(SeedConstants.UserId);

        Assert.NotEmpty(result);
        Assert.Contains(result, at => at.AlbumId == AlbumId1);
    }

    [Fact]
    public async Task GetFavoritePlaylistAsync_ReturnsMaterializedList()
    {
        List<PlaylistTrack> result = await _repository.GetFavoritePlaylistAsync(SeedConstants.UserId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetFavoriteArtistAsync_ReturnsEmptyForUnknownUser()
    {
        List<ArtistTrack> result = await _repository.GetFavoriteArtistAsync(SeedConstants.OtherUserId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFavoriteAlbumAsync_ReturnsEmptyForUnknownUser()
    {
        List<AlbumTrack> result = await _repository.GetFavoriteAlbumAsync(SeedConstants.OtherUserId);

        Assert.Empty(result);
    }

    #endregion

    #region No Disposed Context Tests

    [Fact]
    public async Task BrowsableQueries_DoNotThrowDisposedContextException()
    {
        IQueryable<Artist> artistQuery = _repository.GetArtists(SeedConstants.UserId, "_");
        IQueryable<Album> albumQuery = _repository.GetAlbums(SeedConstants.UserId, "_");
        IQueryable<TrackUser> trackQuery = _repository.GetTracks(SeedConstants.UserId);

        List<Artist> artists = await artistQuery.ToListAsync();
        List<Album> albums = await albumQuery.ToListAsync();
        List<TrackUser> tracks = await trackQuery.ToListAsync();

        Assert.NotNull(artists);
        Assert.NotNull(albums);
        Assert.NotNull(tracks);
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}
