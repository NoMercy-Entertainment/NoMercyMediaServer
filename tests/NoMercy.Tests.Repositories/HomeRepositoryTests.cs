using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
public class HomeRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly HomeRepository _repository;

    public HomeRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new();
    }

    [Fact]
    public async Task GetHomeMovies_ReturnsMoviesById()
    {
        List<Movie> movies = await _repository.GetHomeMovies(
            _context, [550, 680], "en", "US");

        Assert.Equal(2, movies.Count);
        Assert.Contains(movies, m => m.Id == 550);
        Assert.Contains(movies, m => m.Id == 680);
    }

    [Fact]
    public async Task GetHomeMovies_ReturnsEmpty_WhenIdsNotFound()
    {
        List<Movie> movies = await _repository.GetHomeMovies(
            _context, [999999], "en", "US");

        Assert.Empty(movies);
    }

    [Fact]
    public async Task GetHomeTvs_ReturnsTvShowsById()
    {
        List<Tv> shows = await _repository.GetHomeTvs(
            _context, [1399], "en", "US");

        Assert.Single(shows);
        Assert.Equal(1399, shows[0].Id);
    }

    [Fact]
    public async Task GetMovieCountAsync_ReturnsCorrectCount()
    {
        int count = await _repository.GetMovieCountAsync(_context, SeedConstants.UserId);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetTvCountAsync_ReturnsCorrectCount()
    {
        int count = await _repository.GetTvCountAsync(_context, SeedConstants.UserId);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetMovieCountAsync_ReturnsZero_WhenUserHasNoAccess()
    {
        int count = await _repository.GetMovieCountAsync(_context, SeedConstants.OtherUserId);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetLibrariesAsync_ReturnsLibrariesForUser()
    {
        List<Library> libraries = await _repository.GetLibrariesAsync(
            _context, SeedConstants.UserId);

        Assert.Equal(2, libraries.Count);
    }

    [Fact]
    public async Task GetLibrariesAsync_ReturnsEmpty_WhenUserHasNoAccess()
    {
        List<Library> libraries = await _repository.GetLibrariesAsync(
            _context, SeedConstants.OtherUserId);

        Assert.Empty(libraries);
    }

    [Fact]
    public async Task GetHomeGenresAsync_ReturnsGenresForUser()
    {
        List<Genre> genres = await _repository.GetHomeGenresAsync(
            _context, SeedConstants.UserId, "en", 10, 0);

        Assert.Equal(2, genres.Count);
        Assert.Contains(genres, g => g.Name == "Action");
        Assert.Contains(genres, g => g.Name == "Drama");
    }

    [Fact]
    public async Task GetHomeGenresAsync_RespectsPageAndTake()
    {
        List<Genre> genres = await _repository.GetHomeGenresAsync(
            _context, SeedConstants.UserId, "en", 1, 0);

        Assert.Single(genres);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_ReturnsDeduplicated()
    {
        // Seed has 3 UserData rows: 2 for movie 550 (duplicate), 1 for tv 1399
        // DistinctBy on { MovieId, CollectionId, TvId, SpecialId } should yield 2 unique entries
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            _context, SeedConstants.UserId, "en", "US");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, ud => ud.MovieId == 550);
        Assert.Contains(result, ud => ud.TvId == 1399);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_KeepsMostRecentPerGroup()
    {
        // The most recent entry for movie 550 has LastPlayedDate 2026-02-01
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            _context, SeedConstants.UserId, "en", "US");

        UserData? movieEntry = result.FirstOrDefault(ud => ud.MovieId == 550);
        Assert.NotNull(movieEntry);
        Assert.Equal("2026-02-01T10:00:00Z", movieEntry.LastPlayedDate);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_IncludesVideoFile()
    {
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            _context, SeedConstants.UserId, "en", "US");

        Assert.All(result, ud => Assert.NotNull(ud.VideoFile));
    }

    [Fact]
    public async Task GetContinueWatchingAsync_IncludesMovieData()
    {
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            _context, SeedConstants.UserId, "en", "US");

        UserData? movieEntry = result.FirstOrDefault(ud => ud.MovieId == 550);
        Assert.NotNull(movieEntry);
        Assert.NotNull(movieEntry.Movie);
        Assert.NotEmpty(movieEntry.Movie.VideoFiles);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_ReturnsEmpty_WhenNoUserData()
    {
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            _context, SeedConstants.OtherUserId, "en", "US");

        Assert.Empty(result);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
