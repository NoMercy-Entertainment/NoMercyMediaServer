using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

public class GenreRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly GenreRepository _repository;

    public GenreRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new GenreRepository(_context);
    }

    [Fact]
    public async Task GetGenreAsync_ReturnsGenre_WhenUserHasAccess()
    {
        Genre? genre = await _repository.GetGenreAsync(
            SeedConstants.UserId, 18, "en", "US", 10, 0);

        Assert.NotNull(genre);
        Assert.Equal("Drama", genre.Name);
    }

    [Fact]
    public async Task GetGenreAsync_ReturnsNull_WhenGenreDoesNotExist()
    {
        Genre? genre = await _repository.GetGenreAsync(
            SeedConstants.UserId, 999, "en", "US", 10, 0);

        Assert.Null(genre);
    }

    [Fact]
    public async Task GetGenreAsync_IncludesMoviesForUser()
    {
        Genre? genre = await _repository.GetGenreAsync(
            SeedConstants.UserId, 18, "en", "US", 10, 0);

        Assert.NotNull(genre);
        Assert.NotEmpty(genre.GenreMovies);
    }

    [Fact]
    public async Task GetGenreAsync_IncludesTvShowsForUser()
    {
        Genre? genre = await _repository.GetGenreAsync(
            SeedConstants.UserId, 18, "en", "US", 10, 0);

        Assert.NotNull(genre);
        Assert.NotEmpty(genre.GenreTvShows);
    }

    [Fact]
    public async Task GetGenresAsync_ReturnsGenresForUser()
    {
        List<Genre> genres = await _repository.GetGenresAsync(
            SeedConstants.UserId, "en", 10, 0).ToListAsync();

        Assert.Equal(2, genres.Count);
        Assert.Contains(genres, g => g.Name == "Action");
        Assert.Contains(genres, g => g.Name == "Drama");
    }

    [Fact]
    public async Task GetGenresAsync_ReturnsEmpty_WhenUserHasNoAccess()
    {
        List<Genre> genres = await _repository.GetGenresAsync(
            SeedConstants.OtherUserId, "en", 10, 0).ToListAsync();

        Assert.Empty(genres);
    }

    [Fact]
    public async Task GetGenresWithCountsAsync_ReturnsCorrectCounts()
    {
        List<GenreWithCountsDto> genres = await _repository.GetGenresWithCountsAsync(
            SeedConstants.UserId, "en", 10, 0);

        GenreWithCountsDto? dramaGenre = genres.FirstOrDefault(g => g.Name == "Drama");
        Assert.NotNull(dramaGenre);
        Assert.Equal(2, dramaGenre.TotalMovies);
        Assert.Equal(1, dramaGenre.TotalTvShows);
        Assert.Equal(2, dramaGenre.MoviesWithVideo);
        Assert.Equal(1, dramaGenre.TvShowsWithVideo);

        GenreWithCountsDto? actionGenre = genres.FirstOrDefault(g => g.Name == "Action");
        Assert.NotNull(actionGenre);
        Assert.Equal(1, actionGenre.TotalMovies);
        Assert.Equal(0, actionGenre.TotalTvShows);
    }

    [Fact]
    public async Task GetGenresAsync_RespectsPageAndTake()
    {
        List<Genre> genres = await _repository.GetGenresAsync(
            SeedConstants.UserId, "en", 1, 0).ToListAsync();

        Assert.Single(genres);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
