using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Extensions;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Unit")]
public class DiContextInjectionTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly SqliteConnection _keepAliveConnection;

    public DiContextInjectionTests()
    {
        _keepAliveConnection = new SqliteConnection($"DataSource={_dbName};Mode=Memory;Cache=Shared");
        _keepAliveConnection.Open();
        _keepAliveConnection.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        using MediaContext seedContext = CreateContext();
        seedContext.Database.EnsureCreated();
        SeedMusicData(seedContext);
    }

    private MediaContext CreateContext()
    {
        SqliteConnection connection = new($"DataSource={_dbName};Mode=Memory;Cache=Shared");
        connection.Open();
        connection.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(new SqliteNormalizeSearchInterceptor())
            .Options;

        TestMediaContext context = new(options);
        return context;
    }

    private static void SeedMusicData(MediaContext context)
    {
        User user = new()
        {
            Id = SeedConstants.UserId,
            Email = "test@nomercy.tv",
            Name = "Test User",
            Owner = true,
            Allowed = true,
            Manage = true
        };
        context.Users.Add(user);

        Library musicLibrary = new()
        {
            Id = SeedConstants.MovieLibraryId,
            Title = "Music",
            Type = "music",
            Order = 1
        };
        context.Libraries.Add(musicLibrary);
        context.LibraryUser.Add(new LibraryUser(SeedConstants.MovieLibraryId, SeedConstants.UserId));

        Folder musicFolder = new()
        {
            Id = SeedConstants.MovieFolderId,
            Path = "/media/music"
        };
        context.Folders.Add(musicFolder);
        context.FolderLibrary.Add(new FolderLibrary(SeedConstants.MovieFolderId, SeedConstants.MovieLibraryId));

        Artist artist = new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test Artist",
            TitleSort = "test artist",
            Description = "A test artist",
            Cover = "/test.jpg",
            HostFolder = "/media/music/Test Artist",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId
        };
        context.Artists.Add(artist);

        Album album = new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Test Album",
            Description = "A test album",
            Cover = "/test-album.jpg",
            Year = 2020,
            Tracks = 1,
            HostFolder = "/media/music/Test Artist/Test Album",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId
        };
        context.Albums.Add(album);

        context.SaveChanges();

        Track track = new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Test Track",
            TrackNumber = 1,
            DiscNumber = 1,
            Duration = "3:45",
            Filename = "01-test-track.flac",
            Folder = "/media/music/Test Artist/Test Album",
            HostFolder = "/media/music/Test Artist/Test Album",
            FolderId = SeedConstants.MovieFolderId
        };
        context.Tracks.Add(track);

        context.SaveChanges();

        context.ArtistTrack.Add(new ArtistTrack
        {
            ArtistId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TrackId = Guid.Parse("33333333-3333-3333-3333-333333333333")
        });
        context.AlbumTrack.Add(new AlbumTrack
        {
            AlbumId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TrackId = Guid.Parse("33333333-3333-3333-3333-333333333333")
        });
        context.AlbumArtist.Add(new AlbumArtist
        {
            AlbumId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ArtistId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        });
        context.ArtistLibrary.Add(new ArtistLibrary(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SeedConstants.MovieLibraryId));
        context.AlbumLibrary.Add(new AlbumLibrary(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            SeedConstants.MovieLibraryId));

        Playlist playlist = new()
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "Test Playlist",
            Description = "A test playlist",
            UserId = SeedConstants.UserId
        };
        context.Playlists.Add(playlist);

        context.SaveChanges();

        context.PlaylistTrack.Add(new PlaylistTrack
        {
            PlaylistId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            TrackId = Guid.Parse("33333333-3333-3333-3333-333333333333")
        });

        context.SaveChanges();
    }

    [Fact]
    public async Task MusicRepository_UsesInjectedContext_NotNewInstance()
    {
        // Verify MusicRepository queries use the injected context by checking data is accessible
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        List<Guid> artistIds = await repository.SearchArtistIdsAsync("test");
        Assert.Single(artistIds);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), artistIds[0]);
    }

    [Fact]
    public async Task MusicRepository_SearchAlbumIds_UsesInjectedContext()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        List<Guid> albumIds = await repository.SearchAlbumIdsAsync("test");
        Assert.Single(albumIds);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), albumIds[0]);
    }

    [Fact]
    public async Task MusicRepository_SearchTrackIds_UsesInjectedContext()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        List<Guid> trackIds = await repository.SearchTrackIdsAsync("test");
        Assert.Single(trackIds);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), trackIds[0]);
    }

    [Fact]
    public async Task MusicRepository_SearchPlaylistIds_UsesInjectedContext()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        List<Guid> playlistIds = await repository.SearchPlaylistIdsAsync("test");
        Assert.Single(playlistIds);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), playlistIds[0]);
    }

    [Fact]
    public async Task MusicRepository_GetArtistAsync_UsesInjectedContext()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        Artist? artist = await repository.GetArtistAsync(
            SeedConstants.UserId,
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.NotNull(artist);
        Assert.Equal("Test Artist", artist.Name);
    }

    [Fact]
    public async Task DbContextFactory_CreatesDistinctContextsForConcurrentUse()
    {
        // Simulate IDbContextFactory behavior: each factory call returns a distinct context
        // that can be used safely on different threads
        Task<int> task1 = Task.Run(() =>
        {
            using MediaContext context = CreateContext();
            return context.Artists.Count();
        });

        Task<int> task2 = Task.Run(() =>
        {
            using MediaContext context = CreateContext();
            return context.Albums.Count();
        });

        Task<int> task3 = Task.Run(() =>
        {
            using MediaContext context = CreateContext();
            return context.Tracks.Count();
        });

        await Task.WhenAll(task1, task2, task3);

        Assert.Equal(1, await task1);
        Assert.Equal(1, await task2);
        Assert.Equal(1, await task3);
    }

    [Fact]
    public async Task MusicRepository_EmptyContext_ReturnsNoResults()
    {
        // Verify that a repository with no data returns empty results
        // (proves it reads from the injected context, not a global/static one)
        string isolatedDb = Guid.NewGuid().ToString();
        using SqliteConnection isolatedConn = new($"DataSource={isolatedDb};Mode=Memory;Cache=Shared");
        isolatedConn.Open();
        isolatedConn.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(isolatedConn, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(new SqliteNormalizeSearchInterceptor())
            .Options;
        using TestMediaContext emptyContext = new(options);
        emptyContext.Database.EnsureCreated();

        MusicRepository repository = new(emptyContext);

        List<Guid> artistIds = await repository.SearchArtistIdsAsync("test");
        List<Guid> albumIds = await repository.SearchAlbumIdsAsync("test");
        List<Guid> trackIds = await repository.SearchTrackIdsAsync("test");
        List<Guid> playlistIds = await repository.SearchPlaylistIdsAsync("test");

        Assert.Empty(artistIds);
        Assert.Empty(albumIds);
        Assert.Empty(trackIds);
        Assert.Empty(playlistIds);
    }

    public void Dispose()
    {
        _keepAliveConnection.Close();
        _keepAliveConnection.Dispose();
    }
}
