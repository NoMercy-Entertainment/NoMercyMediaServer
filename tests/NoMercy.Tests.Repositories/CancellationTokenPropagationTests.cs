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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Unit")]
public class CancellationTokenPropagationTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _factoryConnection;

    public CancellationTokenPropagationTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        (_factory, _factoryConnection) = TestMediaContextFactory.CreateSeededFactory();
    }

    [Fact]
    public async Task MovieRepository_GetMovieAsync_ThrowsWhenCancelled()
    {
        MovieRepository repository = new(_factory, NullLogger<MovieRepository>.Instance);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetMovieAsync(SeedConstants.UserId, 129, "en", "US", cts.Token)
        );
    }

    [Fact]
    public async Task MovieRepository_GetMovieAvailableAsync_ThrowsWhenCancelled()
    {
        MovieRepository repository = new(_factory, NullLogger<MovieRepository>.Instance);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetMovieAvailableAsync(SeedConstants.UserId, 129, cts.Token)
        );
    }

    [Fact]
    public async Task MovieRepository_GetMoviePlaylistAsync_ThrowsWhenCancelled()
    {
        MovieRepository repository = new(_factory, NullLogger<MovieRepository>.Instance);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetMoviePlaylistAsync(SeedConstants.UserId, 129, "en", "US", cts.Token)
        );
    }

    [Fact]
    public async Task MovieRepository_DeleteMovieAsync_ThrowsWhenCancelled()
    {
        MovieRepository repository = new(_factory, NullLogger<MovieRepository>.Instance);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.DeleteAsync(999999, cts.Token)
        );
    }

    [Fact]
    public async Task TvShowRepository_GetTvAvailableAsync_ThrowsWhenCancelled()
    {
        TvShowRepository repository = new(_factory, new StubMediaTypeClassifier());
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetTvAvailableAsync(SeedConstants.UserId, 1396, cts.Token)
        );
    }

    [Fact]
    public async Task TvShowRepository_DeleteTvAsync_ThrowsWhenCancelled()
    {
        TvShowRepository repository = new(_factory, new StubMediaTypeClassifier());
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.DeleteAsync(999999, cts.Token)
        );
    }

    [Fact]
    public async Task LibraryRepository_GetLibraries_ThrowsWhenCancelled()
    {
        LibraryRepository repository = new(_factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetLibraries(SeedConstants.UserId, cts.Token)
        );
    }

    [Fact]
    public async Task LibraryRepository_GetLibraryMovieCardsAsync_ThrowsWhenCancelled()
    {
        LibraryRepository repository = new(_factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetLibraryMovieCardsAsync(
                SeedConstants.UserId,
                SeedConstants.MovieLibraryId,
                "US",
                10,
                0,
                cts.Token
            )
        );
    }

    [Fact]
    public async Task LibraryRepository_GetLibraryTvCardsAsync_ThrowsWhenCancelled()
    {
        LibraryRepository repository = new(_factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetLibraryTvCardsAsync(
                SeedConstants.UserId,
                SeedConstants.TvLibraryId,
                "US",
                10,
                0,
                cts.Token
            )
        );
    }

    [Fact]
    public async Task CollectionRepository_GetCollectionsListAsync_ThrowsWhenCancelled()
    {
        CollectionRepository repository = new(_factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetCollectionsListAsync(SeedConstants.UserId, "en", "US", 10, 0, cts.Token)
        );
    }

    [Fact]
    public async Task GenreRepository_GetGenresWithCountsAsync_ThrowsWhenCancelled()
    {
        GenreRepository repository = new(_context);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetGenresWithCountsAsync(SeedConstants.UserId, "en", 10, 0, cts.Token)
        );
    }

    [Fact]
    public async Task SpecialRepository_GetSpecialsAsync_ThrowsWhenCancelled()
    {
        SpecialRepository repository = new(_context, _factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetSpecialsAsync(SeedConstants.UserId, "en", 10, 0, cts.Token)
        );
    }

    [Fact]
    public async Task MovieRepository_GetMovieAsync_WorksWithDefaultToken()
    {
        MovieRepository repository = new(_factory, NullLogger<MovieRepository>.Instance);

        Movie? movie = await repository.GetMovieAsync(SeedConstants.UserId, 129, "en", "US");

        Assert.NotNull(movie);
        Assert.Equal("Spirited Away", movie.Title);
    }

    [Fact]
    public async Task TvShowRepository_GetTvAvailableAsync_WorksWithDefaultToken()
    {
        TvShowRepository repository = new(_factory, new StubMediaTypeClassifier());

        bool available = await repository.GetTvAvailableAsync(SeedConstants.UserId, 1399);

        Assert.True(available);
    }

    public void Dispose()
    {
        _context.Dispose();
        _factoryConnection.Dispose();
    }
}
