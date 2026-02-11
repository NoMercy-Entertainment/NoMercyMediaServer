using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Extensions;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Unit")]
public class MusicSearchDbFilterTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly SqliteConnection _keepAliveConnection;

    public MusicSearchDbFilterTests()
    {
        _keepAliveConnection = new SqliteConnection($"DataSource={_dbName};Mode=Memory;Cache=Shared");
        _keepAliveConnection.Open();
        _keepAliveConnection.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        using MediaContext seedContext = CreateContext();
        seedContext.Database.EnsureCreated();
        SeedSearchData(seedContext);
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

        return new TestMediaContext(options);
    }

    private static void SeedSearchData(MediaContext context)
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

        Library library = new()
        {
            Id = SeedConstants.MovieLibraryId,
            Title = "Music",
            Type = "music",
            Order = 1
        };
        context.Libraries.Add(library);

        Folder folder = new()
        {
            Id = SeedConstants.MovieFolderId,
            Path = "/media/music"
        };
        context.Folders.Add(folder);

        // Add all entities in one batch to avoid tracking conflicts
        // (the `= new()` defaults on navigation properties are resolved when parent is in same batch)
        context.Artists.Add(new Artist
        {
            Id = Guid.Parse("a0000001-0000-0000-0000-000000000001"),
            Name = "Beyoncé",
            TitleSort = "beyonce",
            Cover = "/test.jpg",
            HostFolder = "/media/music/Beyonce",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId
        });
        context.Artists.Add(new Artist
        {
            Id = Guid.Parse("a0000001-0000-0000-0000-000000000002"),
            Name = "Mötley Crüe",
            TitleSort = "motley crue",
            Cover = "/test.jpg",
            HostFolder = "/media/music/Motley Crue",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId
        });
        context.Artists.Add(new Artist
        {
            Id = Guid.Parse("a0000001-0000-0000-0000-000000000003"),
            Name = "AC/DC",
            TitleSort = "acdc",
            Cover = "/test.jpg",
            HostFolder = "/media/music/ACDC",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId
        });
        context.Artists.Add(new Artist
        {
            Id = Guid.Parse("a0000001-0000-0000-0000-000000000004"),
            Name = "Twenty—One Pilots",
            TitleSort = "twenty one pilots",
            Cover = "/test.jpg",
            HostFolder = "/media/music/Twenty One Pilots",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId
        });
        context.Artists.Add(new Artist
        {
            Id = Guid.Parse("a0000001-0000-0000-0000-000000000005"),
            Name = "The Rolling Stones",
            TitleSort = "rolling stones",
            Cover = "/test.jpg",
            HostFolder = "/media/music/The Rolling Stones",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = SeedConstants.MovieFolderId
        });

        context.Albums.Add(new Album
        {
            Id = Guid.Parse("b0000001-0000-0000-0000-000000000001"),
            Name = "Résumé",
            Cover = "/test.jpg",
            HostFolder = "/media/music/Resume",
            Library = library,
            LibraryFolder = folder
        });
        context.Albums.Add(new Album
        {
            Id = Guid.Parse("b0000001-0000-0000-0000-000000000002"),
            Name = "Greatest Hits",
            Cover = "/test.jpg",
            HostFolder = "/media/music/Greatest Hits",
            Library = library,
            LibraryFolder = folder
        });

        context.SaveChanges();

        // Second batch: Tracks and Playlists (after Library/Folder are committed)
        context.Tracks.Add(new Track
        {
            Id = Guid.Parse("c0000001-0000-0000-0000-000000000001"),
            Name = "Déjà Vu",
            Filename = "deja_vu.mp3",
            Duration = "3:45",
            Quality = 320,
            Folder = "/media/music/Deja Vu",
            HostFolder = "/media/music/Deja Vu",
            FolderId = SeedConstants.MovieFolderId
        });
        context.Tracks.Add(new Track
        {
            Id = Guid.Parse("c0000001-0000-0000-0000-000000000002"),
            Name = "Rock You Like a Hurricane",
            Filename = "rock_you.mp3",
            Duration = "4:10",
            Quality = 320,
            Folder = "/media/music/Rock",
            HostFolder = "/media/music/Rock",
            FolderId = SeedConstants.MovieFolderId
        });

        context.Playlists.Add(new Playlist
        {
            Id = Guid.Parse("d0000001-0000-0000-0000-000000000001"),
            Name = "Café Vibes",
            UserId = SeedConstants.UserId
        });
        context.Playlists.Add(new Playlist
        {
            Id = Guid.Parse("d0000001-0000-0000-0000-000000000002"),
            Name = "Road Trip",
            UserId = SeedConstants.UserId
        });

        context.SaveChanges();
    }

    [Fact]
    public async Task SearchArtistIdsAsync_AccentedQuery_FindsMatch()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        // "beyonce" should find "Beyoncé" via accent normalization
        List<Guid> ids = await repository.SearchArtistIdsAsync("beyonce");
        Assert.Single(ids);
        Assert.Equal(Guid.Parse("a0000001-0000-0000-0000-000000000001"), ids[0]);
    }

    [Fact]
    public async Task SearchArtistIdsAsync_UmlautQuery_FindsMatch()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        // "motley crue" should find "Mötley Crüe"
        List<Guid> ids = await repository.SearchArtistIdsAsync("motley crue");
        Assert.Single(ids);
        Assert.Equal(Guid.Parse("a0000001-0000-0000-0000-000000000002"), ids[0]);
    }

    [Fact]
    public async Task SearchArtistIdsAsync_EmDashNormalized_FindsMatch()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        // "twenty-one" should find "Twenty—One Pilots" (em dash normalized to hyphen)
        List<Guid> ids = await repository.SearchArtistIdsAsync("twenty-one");
        Assert.Single(ids);
        Assert.Equal(Guid.Parse("a0000001-0000-0000-0000-000000000004"), ids[0]);
    }

    [Fact]
    public async Task SearchArtistIdsAsync_CaseInsensitive_FindsMatch()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        // "rolling stones" should find "The Rolling Stones"
        List<Guid> ids = await repository.SearchArtistIdsAsync("rolling stones");
        Assert.Single(ids);
        Assert.Equal(Guid.Parse("a0000001-0000-0000-0000-000000000005"), ids[0]);
    }

    [Fact]
    public async Task SearchArtistIdsAsync_NoMatch_ReturnsEmpty()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        List<Guid> ids = await repository.SearchArtistIdsAsync("nonexistent artist");
        Assert.Empty(ids);
    }

    [Fact]
    public async Task SearchAlbumIdsAsync_AccentedAlbum_FindsMatch()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        // "resume" should find "Résumé"
        List<Guid> ids = await repository.SearchAlbumIdsAsync("resume");
        Assert.Single(ids);
        Assert.Equal(Guid.Parse("b0000001-0000-0000-0000-000000000001"), ids[0]);
    }

    [Fact]
    public async Task SearchTrackIdsAsync_AccentedTrack_FindsMatch()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        // "deja vu" should find "Déjà Vu"
        List<Guid> ids = await repository.SearchTrackIdsAsync("deja vu");
        Assert.Single(ids);
        Assert.Equal(Guid.Parse("c0000001-0000-0000-0000-000000000001"), ids[0]);
    }

    [Fact]
    public async Task SearchPlaylistIdsAsync_AccentedPlaylist_FindsMatch()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        // "cafe" should find "Café Vibes"
        List<Guid> ids = await repository.SearchPlaylistIdsAsync("cafe");
        Assert.Single(ids);
        Assert.Equal(Guid.Parse("d0000001-0000-0000-0000-000000000001"), ids[0]);
    }

    [Fact]
    public async Task SearchArtistIdsAsync_QueryIsDbSide_NotFullTableScan()
    {
        // Verify the query has a WHERE clause containing normalize_search
        string dbName = Guid.NewGuid().ToString();
        SqliteConnection connection = new($"DataSource={dbName};Mode=Memory;Cache=Shared");
        connection.Open();
        connection.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        SqlCaptureInterceptor interceptor = new();
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(interceptor, new SqliteNormalizeSearchInterceptor())
            .Options;

        using TestMediaContext context = new(options);
        context.Database.EnsureCreated();

        MusicRepository repository = new(context);
        interceptor.Clear();

        await repository.SearchArtistIdsAsync("test");

        // Verify SQL contains normalize_search function call in WHERE clause
        string capturedSql = string.Join(" ", interceptor.CapturedSql);
        Assert.Contains("normalize_search", capturedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", capturedSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchArtistIdsAsync_PartialMatch_FindsMultiple()
    {
        using MediaContext context = CreateContext();
        MusicRepository repository = new(context);

        // "e" should match multiple artists (Beyoncé, Mötley Crüe, Twenty—One Pilots, The Rolling Stones)
        List<Guid> ids = await repository.SearchArtistIdsAsync("e");
        Assert.True(ids.Count > 1, "Partial match 'e' should match multiple artists");
    }

    public void Dispose()
    {
        _keepAliveConnection.Close();
        _keepAliveConnection.Dispose();
    }
}
