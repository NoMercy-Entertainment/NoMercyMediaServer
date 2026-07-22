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
using NoMercy.NmSystem.Extensions;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Unit")]
public class DiContextInjectionTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly SqliteConnection _keepAliveConnection;

    public DiContextInjectionTests()
    {
        _keepAliveConnection = new(connectionString: $"DataSource={_dbName};Mode=Memory;Cache=Shared");
        _keepAliveConnection.Open();
        _keepAliveConnection.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        using MediaContext seedContext = CreateContext();
        seedContext.Database.EnsureCreated();
        SeedMusicData(context: seedContext);
    }

    private MediaContext CreateContext()
    {
        SqliteConnection connection = new(connectionString: $"DataSource={_dbName};Mode=Memory;Cache=Shared");
        connection.Open();
        connection.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connection: connection,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: new SqliteNormalizeSearchInterceptor())
            .Options;

        TestMediaContext context = new(options: options);
        return context;
    }

    private IDbContextFactory<MediaContext> CreateFactory()
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connectionString: $"DataSource={_dbName};Mode=Memory;Cache=Shared",
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: new SqliteNormalizeSearchInterceptor())
            .Options;
        return new TestDbContextFactory(options: options);
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
            Manage = true,
        };
        context.Users.Add(entity: user);

        Library musicLibrary = new()
        {
            Id = SeedConstants.MovieLibraryId,
            Title = "Music",
            Type = "music",
            Order = 1,
        };
        context.Libraries.Add(entity: musicLibrary);
        context.LibraryUser.Add(entity: new(libraryId: SeedConstants.MovieLibraryId, userId: SeedConstants.UserId));

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
            Id = SeedConstants.MovieFolderId,
            Path = "/media/music",
            DriverId = Driver.SystemLocalDriverId,
        };
        context.Folders.Add(entity: musicFolder);
        context.FolderLibrary.Add(entity: new(folderId: SeedConstants.MovieFolderId, libraryId: SeedConstants.MovieLibraryId));

        Artist artist = new()
        {
            Id = Guid.Parse(input: "11111111-1111-1111-1111-111111111111"),
            Name = "Test Artist",
            TitleSort = "test artist",
            Description = "A test artist",
            Cover = "/test.jpg",
            HostFolder = "/media/music/Test Artist",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId,
        };
        context.Artists.Add(entity: artist);

        Album album = new()
        {
            Id = Guid.Parse(input: "22222222-2222-2222-2222-222222222222"),
            Name = "Test Album",
            Description = "A test album",
            Cover = "/test-album.jpg",
            Year = 2020,
            Tracks = 1,
            HostFolder = "/media/music/Test Artist/Test Album",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId,
            LibraryFolder = null!,
        };
        context.Albums.Add(entity: album);

        context.SaveChanges();

        Track track = new()
        {
            Id = Guid.Parse(input: "33333333-3333-3333-3333-333333333333"),
            Name = "Test Track",
            TrackNumber = 1,
            DiscNumber = 1,
            Duration = "3:45",
            Filename = "01-test-track.flac",
            Folder = "/media/music/Test Artist/Test Album",
            HostFolder = "/media/music/Test Artist/Test Album",
            FolderId = SeedConstants.MovieFolderId,
        };
        context.Tracks.Add(entity: track);

        context.SaveChanges();

        context.ArtistTrack.Add(
            entity: new()
            {
                ArtistId = Guid.Parse(input: "11111111-1111-1111-1111-111111111111"),
                TrackId = Guid.Parse(input: "33333333-3333-3333-3333-333333333333"),
            }
        );
        context.AlbumTrack.Add(
            entity: new()
            {
                AlbumId = Guid.Parse(input: "22222222-2222-2222-2222-222222222222"),
                TrackId = Guid.Parse(input: "33333333-3333-3333-3333-333333333333"),
            }
        );
        context.AlbumArtist.Add(
            entity: new()
            {
                AlbumId = Guid.Parse(input: "22222222-2222-2222-2222-222222222222"),
                ArtistId = Guid.Parse(input: "11111111-1111-1111-1111-111111111111"),
            }
        );
        context.ArtistLibrary.Add(
            entity: new(artistId: Guid.Parse(input: "11111111-1111-1111-1111-111111111111"), libraryId: SeedConstants.MovieLibraryId)
        );
        context.AlbumLibrary.Add(
            entity: new(albumId: Guid.Parse(input: "22222222-2222-2222-2222-222222222222"), libraryId: SeedConstants.MovieLibraryId)
        );

        Playlist playlist = new()
        {
            Id = Guid.Parse(input: "44444444-4444-4444-4444-444444444444"),
            Name = "Test Playlist",
            Description = "A test playlist",
            UserId = SeedConstants.UserId,
        };
        context.Playlists.Add(entity: playlist);

        context.SaveChanges();

        context.PlaylistTrack.Add(
            entity: new()
            {
                PlaylistId = Guid.Parse(input: "44444444-4444-4444-4444-444444444444"),
                TrackId = Guid.Parse(input: "33333333-3333-3333-3333-333333333333"),
            }
        );

        context.SaveChanges();
    }

    [Fact]
    public async Task MusicRepository_UsesInjectedFactory_NotNewInstance()
    {
        // Verify MusicRepository queries use the injected factory by checking data is accessible
        MusicRepository repository = new(contextFactory: CreateFactory());

        List<Guid> artistIds = await repository.SearchArtistIdsAsync(normalizedQuery: "test");
        Assert.Single(collection: artistIds);
        Assert.Equal(expected: Guid.Parse(input: "11111111-1111-1111-1111-111111111111"), actual: artistIds[index: 0]);
    }

    [Fact]
    public async Task MusicRepository_SearchAlbumIds_UsesInjectedFactory()
    {
        MusicRepository repository = new(contextFactory: CreateFactory());

        List<Guid> albumIds = await repository.SearchAlbumIdsAsync(normalizedQuery: "test");
        Assert.Single(collection: albumIds);
        Assert.Equal(expected: Guid.Parse(input: "22222222-2222-2222-2222-222222222222"), actual: albumIds[index: 0]);
    }

    [Fact]
    public async Task MusicRepository_SearchTrackIds_UsesInjectedFactory()
    {
        MusicRepository repository = new(contextFactory: CreateFactory());

        List<Guid> trackIds = await repository.SearchTrackIdsAsync(normalizedQuery: "test");
        Assert.Single(collection: trackIds);
        Assert.Equal(expected: Guid.Parse(input: "33333333-3333-3333-3333-333333333333"), actual: trackIds[index: 0]);
    }

    [Fact]
    public async Task MusicRepository_SearchPlaylistIds_UsesInjectedFactory()
    {
        MusicRepository repository = new(contextFactory: CreateFactory());

        List<Guid> playlistIds = await repository.SearchPlaylistIdsAsync(normalizedQuery: "test");
        Assert.Single(collection: playlistIds);
        Assert.Equal(expected: Guid.Parse(input: "44444444-4444-4444-4444-444444444444"), actual: playlistIds[index: 0]);
    }

    [Fact]
    public async Task MusicRepository_GetArtistAsync_UsesInjectedFactory()
    {
        MusicRepository repository = new(contextFactory: CreateFactory());

        Artist? artist = await repository.GetArtistAsync(
            userId: SeedConstants.UserId,
            id: Guid.Parse(input: "11111111-1111-1111-1111-111111111111")
        );

        Assert.NotNull(@object: artist);
        Assert.Equal(expected: "Test Artist", actual: artist.Name);
    }

    [Fact]
    public async Task DbContextFactory_CreatesDistinctContextsForConcurrentUse()
    {
        // Simulate IDbContextFactory behavior: each factory call returns a distinct context
        // that can be used safely on different threads
        Task<int> task1 = Task.Run(function: () =>
        {
            using MediaContext context = CreateContext();
            return context.Artists.Count();
        });

        Task<int> task2 = Task.Run(function: () =>
        {
            using MediaContext context = CreateContext();
            return context.Albums.Count();
        });

        Task<int> task3 = Task.Run(function: () =>
        {
            using MediaContext context = CreateContext();
            return context.Tracks.Count();
        });

        await Task.WhenAll(tasks: [task1, task2, task3]);

        Assert.Equal(expected: 1, actual: await task1);
        Assert.Equal(expected: 1, actual: await task2);
        Assert.Equal(expected: 1, actual: await task3);
    }

    [Fact]
    public async Task MusicRepository_EmptyContext_ReturnsNoResults()
    {
        // Verify that a repository with no data returns empty results
        // (proves it reads from the injected factory, not a global/static one)
        string isolatedDb = Guid.NewGuid().ToString();
        await using SqliteConnection isolatedConn = new(
            connectionString: $"DataSource={isolatedDb};Mode=Memory;Cache=Shared"
        );
        isolatedConn.Open();
        isolatedConn.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connectionString: $"DataSource={isolatedDb};Mode=Memory;Cache=Shared",
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: new SqliteNormalizeSearchInterceptor())
            .Options;
        using (TestMediaContext initContext = new(options: options))
        {
            await initContext.Database.EnsureCreatedAsync();
        }

        MusicRepository repository = new(contextFactory: new TestDbContextFactory(options: options));

        List<Guid> artistIds = await repository.SearchArtistIdsAsync(normalizedQuery: "test");
        List<Guid> albumIds = await repository.SearchAlbumIdsAsync(normalizedQuery: "test");
        List<Guid> trackIds = await repository.SearchTrackIdsAsync(normalizedQuery: "test");
        List<Guid> playlistIds = await repository.SearchPlaylistIdsAsync(normalizedQuery: "test");

        Assert.Empty(collection: artistIds);
        Assert.Empty(collection: albumIds);
        Assert.Empty(collection: trackIds);
        Assert.Empty(collection: playlistIds);
    }

    public void Dispose()
    {
        _keepAliveConnection.Close();
        _keepAliveConnection.Dispose();
    }
}
