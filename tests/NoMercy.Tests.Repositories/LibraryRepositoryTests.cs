using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

public class LibraryRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly LibraryRepository _repository;

    public LibraryRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new LibraryRepository(_context);
    }

    [Fact]
    public async Task GetLibraries_ReturnsLibrariesForUser()
    {
        List<Library> libraries = await _repository.GetLibraries(SeedConstants.UserId);

        Assert.Equal(2, libraries.Count);
        Assert.Contains(libraries, l => l.Title == "Movies");
        Assert.Contains(libraries, l => l.Title == "TV Shows");
    }

    [Fact]
    public async Task GetLibraries_ReturnsEmpty_WhenUserHasNoAccess()
    {
        List<Library> libraries = await _repository.GetLibraries(SeedConstants.OtherUserId);

        Assert.Empty(libraries);
    }

    [Fact]
    public async Task GetLibraries_OrderedByOrder()
    {
        List<Library> libraries = await _repository.GetLibraries(SeedConstants.UserId);

        Assert.Equal("Movies", libraries[0].Title);
        Assert.Equal("TV Shows", libraries[1].Title);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Ulid_ReturnsLibrary()
    {
        Library? library = await _repository.GetLibraryByIdAsync(SeedConstants.MovieLibraryId);

        Assert.NotNull(library);
        Assert.Equal("Movies", library.Title);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Ulid_ReturnsNull_WhenNotFound()
    {
        Library? library = await _repository.GetLibraryByIdAsync(Ulid.NewUlid());

        Assert.Null(library);
    }

    [Fact]
    public async Task GetAllLibrariesAsync_ReturnsAllLibraries()
    {
        List<Library> libraries = await _repository.GetAllLibrariesAsync();

        Assert.Equal(2, libraries.Count);
    }

    [Fact]
    public async Task GetFoldersAsync_ReturnsFolders()
    {
        List<FolderDto> folders = await _repository.GetFoldersAsync();

        Assert.NotEmpty(folders);
    }

    [Fact]
    public async Task GetLibraryMovieCardsAsync_ReturnsMovieCards()
    {
        List<MovieCardDto> cards = await _repository.GetLibraryMovieCardsAsync(
            SeedConstants.UserId, SeedConstants.MovieLibraryId, "US", 10, 0);

        Assert.Equal(2, cards.Count);
        Assert.Contains(cards, c => c.Title == "Fight Club");
        Assert.Contains(cards, c => c.Title == "Pulp Fiction");
    }

    [Fact]
    public async Task GetLibraryMovieCardsAsync_RespectsSkipAndTake()
    {
        List<MovieCardDto> cards = await _repository.GetLibraryMovieCardsAsync(
            SeedConstants.UserId, SeedConstants.MovieLibraryId, "US", 1, 0);

        Assert.Single(cards);
    }

    [Fact]
    public async Task GetLibraryMovieCardsAsync_ReturnsEmpty_WhenUserHasNoAccess()
    {
        List<MovieCardDto> cards = await _repository.GetLibraryMovieCardsAsync(
            SeedConstants.OtherUserId, SeedConstants.MovieLibraryId, "US", 10, 0);

        Assert.Empty(cards);
    }

    [Fact]
    public async Task GetLibraryTvCardsAsync_ReturnsTvCards()
    {
        List<TvCardDto> cards = await _repository.GetLibraryTvCardsAsync(
            SeedConstants.UserId, SeedConstants.TvLibraryId, "US", 10, 0);

        Assert.Single(cards);
        Assert.Equal("Breaking Bad", cards[0].Title);
    }

    [Fact]
    public async Task AddLibraryAsync_CreatesLibrary()
    {
        Ulid newLibraryId = Ulid.NewUlid();
        Library newLibrary = new()
        {
            Id = newLibraryId,
            Title = "Music",
            Type = "music",
            Order = 3
        };

        await _repository.AddLibraryAsync(newLibrary, SeedConstants.UserId);

        Library? found = await _repository.GetLibraryByIdAsync(newLibraryId);
        Assert.NotNull(found);
        Assert.Equal("Music", found.Title);
    }

    [Fact]
    public async Task DeleteLibraryAsync_RemovesLibrary()
    {
        Library? library = await _context.Libraries
            .FirstOrDefaultAsync(l => l.Id == SeedConstants.MovieLibraryId);
        Assert.NotNull(library);

        await _repository.DeleteLibraryAsync(library);

        Library? deleted = await _repository.GetLibraryByIdAsync(SeedConstants.MovieLibraryId);
        Assert.Null(deleted);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
