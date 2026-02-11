using Microsoft.EntityFrameworkCore;
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

[Trait("Category", "Characterization")]
public class TestMediaContextFactoryTests : IDisposable
{
    private readonly MediaContext _context;

    public TestMediaContextFactoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
    }

    [Fact]
    public void CreateContext_CreatesEmptyDatabase()
    {
        using MediaContext emptyContext = TestMediaContextFactory.CreateContext();
        Assert.Empty(emptyContext.Users);
    }

    [Fact]
    public void CreateSeededContext_SeedsUser()
    {
        User? user = _context.Users.FirstOrDefault(u => u.Id == SeedConstants.UserId);
        Assert.NotNull(user);
        Assert.Equal("Test User", user.Name);
        Assert.True(user.Owner);
    }

    [Fact]
    public void CreateSeededContext_SeedsLibraries()
    {
        List<Library> libraries = _context.Libraries.ToList();
        Assert.Equal(2, libraries.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsLibraryUserAccess()
    {
        List<LibraryUser> libraryUsers = _context.LibraryUser
            .Where(lu => lu.UserId == SeedConstants.UserId)
            .ToList();
        Assert.Equal(2, libraryUsers.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsMovies()
    {
        List<Movie> movies = _context.Movies.ToList();
        Assert.Equal(2, movies.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsTvShows()
    {
        List<Tv> shows = _context.Tvs.ToList();
        Assert.Single(shows);
    }

    [Fact]
    public void CreateSeededContext_SeedsVideoFiles()
    {
        List<VideoFile> videoFiles = _context.VideoFiles.ToList();
        Assert.Equal(4, videoFiles.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsEpisodes()
    {
        List<Episode> episodes = _context.Episodes.ToList();
        Assert.Equal(2, episodes.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsGenres()
    {
        List<Genre> genres = _context.Genres.ToList();
        Assert.Equal(2, genres.Count);
    }

    [Fact]
    public async Task CreateSeededContext_MovieLibraryJoinWorks()
    {
        List<LibraryMovie> libraryMovies = await _context.LibraryMovie.ToListAsync();
        Assert.Equal(2, libraryMovies.Count);
    }

    [Fact]
    public async Task CreateSeededContext_TvLibraryJoinWorks()
    {
        List<LibraryTv> libraryTvs = await _context.LibraryTv.ToListAsync();
        Assert.Single(libraryTvs);
    }

    [Fact]
    public void EachContextIsIsolated()
    {
        using MediaContext context1 = TestMediaContextFactory.CreateSeededContext();
        using MediaContext context2 = TestMediaContextFactory.CreateContext();

        Assert.NotEmpty(context1.Users);
        Assert.Empty(context2.Users);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
