using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Extensions;
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
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

public class ForUserExtensionTests
{
    private readonly Guid _userId = SeedConstants.UserId;
    private readonly Guid _otherUserId = SeedConstants.OtherUserId;

    [Fact]
    public async Task ForUser_Movie_ReturnsOnlyAccessibleMovies()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Movie> movies = await context.Movies
            .AsNoTracking()
            .ForUser(_userId)
            .ToListAsync();

        Assert.Equal(2, movies.Count);
        Assert.Contains(movies, m => m.Title == "Fight Club");
        Assert.Contains(movies, m => m.Title == "Pulp Fiction");
    }

    [Fact]
    public async Task ForUser_Movie_ExcludesUnauthorizedUser()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Movie> movies = await context.Movies
            .AsNoTracking()
            .ForUser(_otherUserId)
            .ToListAsync();

        Assert.Empty(movies);
    }

    [Fact]
    public async Task ForUser_Tv_ReturnsOnlyAccessibleShows()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Tv> shows = await context.Tvs
            .AsNoTracking()
            .ForUser(_userId)
            .ToListAsync();

        Assert.Single(shows);
        Assert.Equal("Breaking Bad", shows[0].Title);
    }

    [Fact]
    public async Task ForUser_Tv_ExcludesUnauthorizedUser()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Tv> shows = await context.Tvs
            .AsNoTracking()
            .ForUser(_otherUserId)
            .ToListAsync();

        Assert.Empty(shows);
    }

    [Fact]
    public async Task ForUser_Library_ReturnsOnlyAccessibleLibraries()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Library> libraries = await context.Libraries
            .AsNoTracking()
            .ForUser(_userId)
            .ToListAsync();

        Assert.Equal(2, libraries.Count);
        Assert.Contains(libraries, l => l.Title == "Movies");
        Assert.Contains(libraries, l => l.Title == "TV Shows");
    }

    [Fact]
    public async Task ForUser_Library_ExcludesUnauthorizedUser()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Library> libraries = await context.Libraries
            .AsNoTracking()
            .ForUser(_otherUserId)
            .ToListAsync();

        Assert.Empty(libraries);
    }

    [Fact]
    public async Task ForUser_Collection_ReturnsOnlyAccessibleCollections()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        // Add a collection in the movie library
        Collection collection = new()
        {
            Id = 1001,
            Title = "Test Collection",
            TitleSort = "test collection",
            LibraryId = SeedConstants.MovieLibraryId
        };
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        List<Collection> collections = await context.Collections
            .AsNoTracking()
            .ForUser(_userId)
            .ToListAsync();

        Assert.Single(collections);
        Assert.Equal("Test Collection", collections[0].Title);

        List<Collection> otherUserCollections = await context.Collections
            .AsNoTracking()
            .ForUser(_otherUserId)
            .ToListAsync();

        Assert.Empty(otherUserCollections);
    }

    [Fact]
    public async Task ForUser_Album_ReturnsOnlyAccessibleAlbums()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        // Add an album to the existing movie library (has user access already seeded)
        Folder folder = await context.Folders.FirstAsync();
        context.Albums.Add(new Album
        {
            Id = Guid.NewGuid(),
            Name = "Test Album",
            LibraryId = SeedConstants.MovieLibraryId,
            FolderId = folder.Id,
            Library = null!
        });
        await context.SaveChangesAsync();

        List<Album> albums = await context.Albums
            .AsNoTracking()
            .ForUser(_userId)
            .ToListAsync();

        Assert.Single(albums);
        Assert.Equal("Test Album", albums[0].Name);

        List<Album> otherUserAlbums = await context.Albums
            .AsNoTracking()
            .ForUser(_otherUserId)
            .ToListAsync();

        Assert.Empty(otherUserAlbums);
    }

    [Fact]
    public async Task ForUser_Artist_ReturnsOnlyAccessibleArtists()
    {
        MediaContext context = TestMediaContextFactory.CreateContext();

        // Set up music library with user access
        Ulid musicLibraryId = Ulid.NewUlid();
        Ulid musicFolderId = Ulid.NewUlid();
        context.Libraries.Add(new Library
        {
            Id = musicLibraryId,
            Title = "Music",
            Type = "music",
            Order = 3
        });
        context.Folders.Add(new Folder
        {
            Id = musicFolderId,
            Path = "/media/music"
        });
        context.Users.Add(new User
        {
            Id = _userId,
            Email = "test@nomercy.tv",
            Name = "Test User",
            Owner = true,
            Allowed = true
        });
        context.LibraryUser.Add(new LibraryUser(musicLibraryId, _userId));
        context.Artists.Add(new Artist
        {
            Id = Guid.NewGuid(),
            Name = "Test Artist",
            HostFolder = "/media/music/TestArtist",
            LibraryId = musicLibraryId,
            FolderId = musicFolderId
        });
        await context.SaveChangesAsync();

        List<Artist> artists = await context.Artists
            .AsNoTracking()
            .ForUser(_userId)
            .ToListAsync();

        Assert.Single(artists);
        Assert.Equal("Test Artist", artists[0].Name);

        List<Artist> otherUserArtists = await context.Artists
            .AsNoTracking()
            .ForUser(_otherUserId)
            .ToListAsync();

        Assert.Empty(otherUserArtists);
    }

    [Fact]
    public async Task ForUser_ChainsWithOtherLinqOperators()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        Movie? movie = await context.Movies
            .AsNoTracking()
            .Where(m => m.Id == 550)
            .ForUser(_userId)
            .FirstOrDefaultAsync();

        Assert.NotNull(movie);
        Assert.Equal("Fight Club", movie.Title);
    }

    [Fact]
    public async Task ForUser_WorksWithCountAndAggregates()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        int movieCount = await context.Movies
            .AsNoTracking()
            .ForUser(_userId)
            .CountAsync();

        Assert.Equal(2, movieCount);

        int otherUserCount = await context.Movies
            .AsNoTracking()
            .ForUser(_otherUserId)
            .CountAsync();

        Assert.Equal(0, otherUserCount);
    }

    [Fact]
    public async Task ForUser_MultipleLibraryAccess_ReturnsFromAllLibraries()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        // The seeded user has access to both movie and TV libraries
        // ForUser on Movies should return movies, ForUser on Tvs should return shows
        int movieCount = await context.Movies.AsNoTracking().ForUser(_userId).CountAsync();
        int tvCount = await context.Tvs.AsNoTracking().ForUser(_userId).CountAsync();

        Assert.Equal(2, movieCount);
        Assert.Equal(1, tvCount);
    }

    [Fact]
    public async Task ForUser_PartialLibraryAccess_OnlyReturnsAccessibleContent()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        // Add a second user with access to only the TV library
        Guid partialUserId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = partialUserId,
            Email = "partial@nomercy.tv",
            Name = "Partial User",
            Owner = false,
            Allowed = true
        });
        context.LibraryUser.Add(new LibraryUser(SeedConstants.TvLibraryId, partialUserId));
        await context.SaveChangesAsync();

        // Partial user should see TV shows but not movies
        List<Movie> movies = await context.Movies.AsNoTracking().ForUser(partialUserId).ToListAsync();
        List<Tv> shows = await context.Tvs.AsNoTracking().ForUser(partialUserId).ToListAsync();

        Assert.Empty(movies);
        Assert.Single(shows);
        Assert.Equal("Breaking Bad", shows[0].Title);
    }

    [Fact]
    public async Task ForUser_GeneratesExistsClauseInSql()
    {
        (MediaContext context, SqlCaptureInterceptor interceptor) = TestMediaContextFactory.CreateSeededContextWithInterceptor();

        await context.Movies
            .AsNoTracking()
            .ForUser(_userId)
            .ToListAsync();

        string sql = string.Join(" ", interceptor.CapturedSql);
        Assert.Contains("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LibraryUser", sql, StringComparison.OrdinalIgnoreCase);
    }
}
