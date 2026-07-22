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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Movies;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Factory;
using NoMercy.Tests.Common;

namespace NoMercy.Tests.MediaProcessing.Movies;

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

        Mock<JobDispatcher> jobDispatcherMock = new();
        MovieResponseMocks mockDataProvider = new();

        _movieRepositoryMock = new();
        _movieClientMock = new();

        IStorageDriver storageDriver = new LocalStorageDriver();
        IStorageFactory storageFactory = new StorageFactory(
            driver: storageDriver,
            logger: NullLogger<StorageFactory>.Instance
        );
        _movieManager = new(
            movieRepository: _movieRepositoryMock.Object,
            jobDispatcher: jobDispatcherMock.Object,
            storageFactory: storageFactory,
            logger: NullLogger<MovieManager>.Instance
        );
        _movieAppends = mockDataProvider.MockMovieAppendsResponse()!;
        _library = new() { Id = new(), Title = "Test Library" };
        _movieId = 1771;
    }

    [Fact]
    public async Task AddMovieAsync_ShouldAddMovie()
    {
        // Arrange
        _movieClientMock.Setup(expression: client => client.WithAllAppends(false)).ReturnsAsync(value: _movieAppends);

        Movie capturedMovie = null!;

        _movieRepositoryMock
            .Setup(expression: repo => repo.Add(It.IsAny<Movie>()))
            .Callback<Movie>(action: movie => capturedMovie = movie);

        // Act
        await _movieManager.Add(id: _movieId, library: _library);

        // Assert
        _movieRepositoryMock.Verify(expression: repo => repo.Add(It.IsAny<Movie>()), times: Times.Once);
        _movieRepositoryMock.Verify(
            expression: repo => repo.LinkToLibrary(_library, It.IsAny<Movie>()),
            times: Times.Once
        );
        Assert.NotNull(@object: capturedMovie);
        Assert.Equal(expected: _movieId, actual: capturedMovie.Id);
        Assert.Equal(expected: _movieAppends.Title, actual: capturedMovie.Title);
    }

    [Fact]
    public async Task UpdateMovieAsync_ShouldRefreshMovieViaUpsert()
    {
        Movie capturedMovie = null!;
        _movieRepositoryMock
            .Setup(expression: repo => repo.Add(It.IsAny<Movie>()))
            .Callback<Movie>(action: movie => capturedMovie = movie);

        await _movieManager.Update(id: _movieId, library: _library);

        _movieRepositoryMock.Verify(expression: repo => repo.Add(It.IsAny<Movie>()), times: Times.Once);
        Assert.NotNull(@object: capturedMovie);
        Assert.Equal(expected: _movieId, actual: capturedMovie.Id);
    }

    [Fact]
    public async Task RemoveMovieAsync_ShouldRemoveViaRepository()
    {
        _movieRepositoryMock.Setup(expression: repo => repo.Remove(_movieId)).Returns(value: Task.CompletedTask);

        await _movieManager.Remove(id: _movieId, library: _library);

        _movieRepositoryMock.Verify(expression: repo => repo.Remove(_movieId), times: Times.Once);
    }

    [Fact]
    public async Task StoreAlternativeTitles_ShouldStoreTitles()
    {
        await _movieManager.StoreAlternativeTitles(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreAlternativeTitles(It.IsAny<IEnumerable<AlternativeTitle>>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task StoreTranslations_ShouldStoreTranslations()
    {
        await _movieManager.StoreTranslations(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreTranslations(It.IsAny<IEnumerable<Translation>>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task StoreContentRatings_ShouldStoreRatings()
    {
        await _movieManager.StoreContentRatings(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreContentRatings(It.IsAny<IEnumerable<CertificationMovie>>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task StoreSimilar_ShouldStoreSimilarMovies()
    {
        await _movieManager.StoreSimilar(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreSimilar(It.IsAny<IEnumerable<Similar>>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task StoreRecommendations_ShouldStoreRecommendations()
    {
        await _movieManager.StoreRecommendations(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreRecommendations(It.IsAny<IEnumerable<Recommendation>>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task StoreVideos_ShouldStoreVideos()
    {
        await _movieManager.StoreVideos(movie: _movieAppends);

        _movieRepositoryMock.Verify(expression: m => m.StoreVideos(It.IsAny<IEnumerable<Media>>()), times: Times.Once);
    }

    [Fact]
    public async Task StoreImages_ShouldStoreImages()
    {
        await _movieManager.StoreImages(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreImages(It.IsAny<IEnumerable<Image>>()),
            times: Times.Exactly(callCount: 3)
        );
    }

    [Fact]
    public async Task StoreKeywords_ShouldStoreKeywords()
    {
        await _movieManager.StoreKeywords(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreKeywords(It.IsAny<IEnumerable<Keyword>>()),
            times: Times.Once
        );
        _movieRepositoryMock.Verify(
            expression: m => m.LinkKeywordsToMovie(It.IsAny<IEnumerable<KeywordMovie>>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task StoreGenres_ShouldStoreGenres()
    {
        await _movieManager.StoreGenres(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreGenres(It.IsAny<IEnumerable<GenreMovie>>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task StoreWatchProviders_ShouldStoreWatchProviders()
    {
        await _movieManager.StoreWatchProviders(movie: _movieAppends);

        _movieRepositoryMock.Verify(
            expression: m => m.StoreWatchProviders(It.IsAny<List<WatchProvider>>()),
            times: Times.Once
        );
        _movieRepositoryMock.Verify(
            expression: m => m.StoreWatchProviderMedias(It.IsAny<List<WatchProviderMedia>>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task StoreCompanies_ShouldStoreCompanies()
    {
        await _movieManager.StoreCompanies(movie: _movieAppends);

        _movieRepositoryMock.Verify(expression: m => m.StoreCompanies(It.IsAny<List<Company>>()), times: Times.Once);
        _movieRepositoryMock.Verify(
            expression: m => m.StoreCompanyMovies(It.IsAny<List<CompanyMovie>>()),
            times: Times.Once
        );
    }
}
