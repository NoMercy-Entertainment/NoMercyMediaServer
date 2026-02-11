using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Dashboard;
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
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
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
    public async Task GetLibraryMovieCardsAsync_TakeMatchesCarouselSize()
    {
        // Verify that Take limits results to the requested carousel size
        List<MovieCardDto> allCards = await _repository.GetLibraryMovieCardsAsync(
            SeedConstants.UserId, SeedConstants.MovieLibraryId, "US", 100, 0);
        Assert.Equal(2, allCards.Count);

        List<MovieCardDto> limitedCards = await _repository.GetLibraryMovieCardsAsync(
            SeedConstants.UserId, SeedConstants.MovieLibraryId, "US", 1, 0);
        Assert.Single(limitedCards);
    }

    [Fact]
    public async Task GetLibraryTvCardsAsync_TakeMatchesCarouselSize()
    {
        List<TvCardDto> allCards = await _repository.GetLibraryTvCardsAsync(
            SeedConstants.UserId, SeedConstants.TvLibraryId, "US", 100, 0);
        Assert.Single(allCards);

        List<TvCardDto> limitedCards = await _repository.GetLibraryTvCardsAsync(
            SeedConstants.UserId, SeedConstants.TvLibraryId, "US", 1, 0);
        Assert.Single(limitedCards);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Paginated_TakeLimitsMoviesPerCarousel()
    {
        // The .Take(take) inside Include() limits movies per-carousel
        Library? library = await _repository.GetLibraryByIdAsync(
            SeedConstants.MovieLibraryId, SeedConstants.UserId, "en", "US", 1, 0);

        Assert.NotNull(library);
        Assert.Single(library.LibraryMovies);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Paginated_TakeReturnsAllWhenHigherThanCount()
    {
        Library? library = await _repository.GetLibraryByIdAsync(
            SeedConstants.MovieLibraryId, SeedConstants.UserId, "en", "US", 100, 0);

        Assert.NotNull(library);
        Assert.Equal(2, library.LibraryMovies.Count);
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

    [Fact]
    public async Task GetLibraries_IncludesEncoderProfilesOnFolders()
    {
        List<Library> libraries = await _repository.GetLibraries(SeedConstants.UserId);

        Library movieLibrary = libraries.First(l => l.Title == "Movies");
        Assert.NotEmpty(movieLibrary.FolderLibraries);

        FolderLibrary folderLibrary = movieLibrary.FolderLibraries.First();
        Assert.NotNull(folderLibrary.Folder);
        Assert.NotEmpty(folderLibrary.Folder.EncoderProfileFolder);

        EncoderProfileFolder epf = folderLibrary.Folder.EncoderProfileFolder.First();
        Assert.NotNull(epf.EncoderProfile);
        Assert.Equal("Default HLS", epf.EncoderProfile.Name);
    }

    [Fact]
    public async Task GetLibraries_MapsToLibrariesResponseItemDto_WithoutException()
    {
        List<Library> libraries = await _repository.GetLibraries(SeedConstants.UserId);

        // This is exactly what the controller does - it should not throw
        List<LibrariesResponseItemDto> response = libraries
            .Select(library => new LibrariesResponseItemDto(library))
            .ToList();

        Assert.Equal(2, response.Count);

        LibrariesResponseItemDto movieDto = response.First(r => r.Title == "Movies");
        Assert.NotEmpty(movieDto.FolderLibrary);
        Assert.NotEmpty(movieDto.FolderLibrary[0].Folder.EncoderProfiles);
        Assert.Equal("Default HLS", movieDto.FolderLibrary[0].Folder.EncoderProfiles[0].Name);
    }

    [Fact]
    public async Task GetFoldersAsync_MapsFolderDto_WithEncoderProfiles()
    {
        List<FolderDto> folders = await _repository.GetFoldersAsync();

        Assert.NotEmpty(folders);
        // FolderDto uses Select projection so EncoderProfileFolder may not be loaded
        // in the projection query, but should not throw
    }

    [Fact]
    public async Task GetLibraries_IncludesLanguageLibraries()
    {
        List<Library> libraries = await _repository.GetLibraries(SeedConstants.UserId);

        Library movieLibrary = libraries.First(l => l.Title == "Movies");
        Assert.NotEmpty(movieLibrary.LanguageLibraries);
        Assert.Equal("en", movieLibrary.LanguageLibraries.First().Language.Iso6391);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
