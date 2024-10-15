using JetBrains.Annotations;
using Moq;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Movies;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Client.Mocks;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Tests.MediaProcessing.Movies;

[TestSubject(typeof(MovieManagerTests))]
public class MovieManagerTests
{
    private readonly Mock<IMovieRepository> _movieRepositoryMock;
    private readonly Mock<ITmdbMovieClient> _movieClientMock;
    private readonly MovieManager _movieManager;
    private readonly TmdbMovieAppends _movieAppends;
    private readonly Library _library;
    private readonly int _movieId;

    public MovieManagerTests()
    {
        // TODO not using the app files and api info.
        AppFiles.CreateAppFolders().Wait();
        ApiInfo.RequestInfo().Wait();
        
        Mock<JobDispatcher> jobDispatcherMock = new();
        MovieResponseMocks mockDataProvider = new();

        _movieRepositoryMock = new Mock<IMovieRepository>();
        _movieClientMock = new Mock<ITmdbMovieClient>();

        _movieManager = new MovieManager(_movieRepositoryMock.Object, jobDispatcherMock.Object);
        _movieAppends = mockDataProvider.MockMovieAppendsResponse()!;
        _library = new Library { Id = new Ulid(), Title = "Test Library" };
        _movieId = 1771;
    }

    [Fact]
    public async Task AddMovieAsync_ShouldAddMovie()
    {
        // Arrange
        _movieClientMock.Setup(client => client.WithAllAppends(false)).ReturnsAsync(_movieAppends);

        Movie capturedMovie = null!;

        _movieRepositoryMock.Setup(repo => repo.Add(It.IsAny<Movie>()))
            .Callback<Movie>(movie => capturedMovie = movie);

        // Act
        await _movieManager.Add(_movieId, _library);

        // Assert
        _movieRepositoryMock.Verify(repo => repo.Add(It.IsAny<Movie>()), Times.Once);
        _movieRepositoryMock.Verify(repo => repo.LinkToLibrary(_library, It.IsAny<Movie>()), Times.Once);
        Assert.NotNull(capturedMovie);
        Assert.Equal(_movieId, capturedMovie.Id);
        Assert.Equal(_movieAppends.Title, capturedMovie.Title);
    }

    [Fact]
    public async Task UpdateMovieAsync_ShouldThrowNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() => _movieManager.Update(_movieId, _library));
    }

    [Fact]
    public async Task RemoveMovieAsync_ShouldThrowNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() => _movieManager.Remove(_movieId, _library));
    }

    [Fact]
    public async Task StoreAlternativeTitles_ShouldStoreTitles()
    {
        await _movieManager.StoreAlternativeTitles(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreAlternativeTitles(It.IsAny<IEnumerable<AlternativeTitle>>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreTranslations_ShouldStoreTranslations()
    {
        await _movieManager.StoreTranslations(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreTranslations(It.IsAny<IEnumerable<Translation>>()), Times.Once);
    }

    [Fact]
    public async Task StoreContentRatings_ShouldStoreRatings()
    {
        await _movieManager.StoreContentRatings(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreContentRatings(It.IsAny<IEnumerable<CertificationMovie>>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreSimilar_ShouldStoreSimilarMovies()
    {
        await _movieManager.StoreSimilar(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreSimilar(It.IsAny<IEnumerable<Similar>>()), Times.Once);
    }

    [Fact]
    public async Task StoreRecommendations_ShouldStoreRecommendations()
    {
        await _movieManager.StoreRecommendations(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreRecommendations(It.IsAny<IEnumerable<Recommendation>>()), Times.Once);
    }

    [Fact]
    public async Task StoreVideos_ShouldStoreVideos()
    {
        await _movieManager.StoreVideos(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreVideos(It.IsAny<IEnumerable<Media>>()), Times.Once);
    }

    [Fact]
    public async Task StoreImages_ShouldStoreImages()
    {
        await _movieManager.StoreImages(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreImages(It.IsAny<IEnumerable<Image>>()), Times.Exactly(3));
    }

    [Fact]
    public async Task StoreKeywords_ShouldStoreKeywords()
    {
        await _movieManager.StoreKeywords(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreKeywords(It.IsAny<IEnumerable<Keyword>>()), Times.Once);
        _movieRepositoryMock.Verify(m => m.LinkKeywordsToMovie(It.IsAny<IEnumerable<KeywordMovie>>()), Times.Once);
    }

    [Fact]
    public async Task StoreGenres_ShouldStoreGenres()
    {
        await _movieManager.StoreGenres(_movieAppends);

        _movieRepositoryMock.Verify(m => m.StoreGenres(It.IsAny<IEnumerable<GenreMovie>>()), Times.Once);
    }

    [Fact]
    public async Task StoreWatchProviders_ShouldStoreWatchProviders()
    {
        await _movieManager.StoreWatchProviders(_movieAppends);

        // No repository call, just log
    }

    [Fact]
    public async Task StoreNetworks_ShouldStoreNetworks()
    {
        await _movieManager.StoreNetworks(_movieAppends);

        // No repository call, just log
    }

    [Fact]
    public async Task StoreCompanies_ShouldStoreCompanies()
    {
        await _movieManager.StoreCompanies(_movieAppends);

        // No repository call, just log
    }
}