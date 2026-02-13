using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
public class MovieRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly MovieRepository _repository;

    public MovieRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(_context);
    }

    [Fact]
    public async Task GetMovieAsync_ReturnsMovie_WhenUserHasAccess()
    {
        Movie? movie = await _repository.GetMovieAsync(
            SeedConstants.UserId, 550, "en", "US");

        Assert.NotNull(movie);
        Assert.Equal(550, movie.Id);
        Assert.Equal("Fight Club", movie.Title);
    }

    [Fact]
    public async Task GetMovieAsync_ReturnsNull_WhenUserHasNoAccess()
    {
        Movie? movie = await _repository.GetMovieAsync(
            SeedConstants.OtherUserId, 550, "en", "US");

        Assert.Null(movie);
    }

    [Fact]
    public async Task GetMovieAsync_ReturnsNull_WhenMovieDoesNotExist()
    {
        Movie? movie = await _repository.GetMovieAsync(
            SeedConstants.UserId, 999999, "en", "US");

        Assert.Null(movie);
    }

    [Fact]
    public async Task GetMovieAsync_IncludesVideoFiles()
    {
        Movie? movie = await _repository.GetMovieAsync(
            SeedConstants.UserId, 550, "en", "US");

        Assert.NotNull(movie);
        Assert.NotEmpty(movie.VideoFiles);
        Assert.Contains(movie.VideoFiles, vf => vf.Filename == "Fight.Club.1999.1080p.mkv");
    }

    [Fact]
    public async Task GetMovieAvailableAsync_ReturnsTrue_WhenMovieHasVideoFiles()
    {
        bool available = await _repository.GetMovieAvailableAsync(
            SeedConstants.UserId, 550);

        Assert.True(available);
    }

    [Fact]
    public async Task GetMovieAvailableAsync_ReturnsFalse_WhenUserHasNoAccess()
    {
        bool available = await _repository.GetMovieAvailableAsync(
            SeedConstants.OtherUserId, 550);

        Assert.False(available);
    }

    [Fact]
    public async Task GetMoviePlaylistAsync_ReturnsMovieWithVideoFiles()
    {
        List<Movie> playlist = await _repository.GetMoviePlaylistAsync(
            SeedConstants.UserId, 550, "en", "US");

        Assert.NotEmpty(playlist);
        Assert.Equal(550, playlist[0].Id);
        Assert.NotEmpty(playlist[0].VideoFiles);
    }

    [Fact]
    public async Task DeleteMovieAsync_RemovesMovie()
    {
        await _repository.DeleteMovieAsync(550);

        Movie? movie = await _repository.GetMovieAsync(
            SeedConstants.UserId, 550, "en", "US");

        Assert.Null(movie);
    }

    [Fact]
    public async Task LikeMovieAsync_AddsMovieUser_WhenLikeIsTrue()
    {
        bool result = await _repository.LikeMovieAsync(550, SeedConstants.UserId, true);

        Assert.True(result);

        MovieUser? movieUser = _context.MovieUser
            .FirstOrDefault(mu => mu.MovieId == 550 && mu.UserId == SeedConstants.UserId);
        Assert.NotNull(movieUser);
    }

    [Fact]
    public async Task LikeMovieAsync_RemovesMovieUser_WhenLikeIsFalse()
    {
        await _repository.LikeMovieAsync(550, SeedConstants.UserId, true);
        bool result = await _repository.LikeMovieAsync(550, SeedConstants.UserId, false);

        Assert.True(result);

        MovieUser? movieUser = _context.MovieUser
            .FirstOrDefault(mu => mu.MovieId == 550 && mu.UserId == SeedConstants.UserId);
        Assert.Null(movieUser);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
