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

[Trait(name: "Category", value: "Unit")]
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
        MovieRepository repository = new(contextFactory: _factory, logger: NullLogger<MovieRepository>.Instance);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetMovieAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US", ct: cts.Token)
        );
    }

    [Fact]
    public async Task MovieRepository_GetMovieAvailableAsync_ThrowsWhenCancelled()
    {
        MovieRepository repository = new(contextFactory: _factory, logger: NullLogger<MovieRepository>.Instance);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetMovieAvailableAsync(userId: SeedConstants.UserId, id: 129, ct: cts.Token)
        );
    }

    [Fact]
    public async Task MovieRepository_GetMoviePlaylistAsync_ThrowsWhenCancelled()
    {
        MovieRepository repository = new(contextFactory: _factory, logger: NullLogger<MovieRepository>.Instance);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetMoviePlaylistAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US", ct: cts.Token)
        );
    }

    [Fact]
    public async Task MovieRepository_DeleteMovieAsync_ThrowsWhenCancelled()
    {
        MovieRepository repository = new(contextFactory: _factory, logger: NullLogger<MovieRepository>.Instance);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.DeleteAsync(id: 999999, ct: cts.Token)
        );
    }

    [Fact]
    public async Task TvShowRepository_GetTvAvailableAsync_ThrowsWhenCancelled()
    {
        TvShowRepository repository = new(contextFactory: _factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetTvAvailableAsync(userId: SeedConstants.UserId, id: 1396, ct: cts.Token)
        );
    }

    [Fact]
    public async Task TvShowRepository_DeleteTvAsync_ThrowsWhenCancelled()
    {
        TvShowRepository repository = new(contextFactory: _factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.DeleteAsync(id: 999999, ct: cts.Token)
        );
    }

    [Fact]
    public async Task LibraryRepository_GetLibraries_ThrowsWhenCancelled()
    {
        LibraryRepository repository = new(contextFactory: _factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetLibraries(userId: SeedConstants.UserId, ct: cts.Token)
        );
    }

    [Fact]
    public async Task LibraryRepository_GetLibraryMovieCardsAsync_ThrowsWhenCancelled()
    {
        LibraryRepository repository = new(contextFactory: _factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetLibraryMovieCardsAsync(
                userId: SeedConstants.UserId,
                libraryId: SeedConstants.MovieLibraryId,
                country: "US",
                take: 10,
                skip: 0,
                ct: cts.Token
            )
        );
    }

    [Fact]
    public async Task LibraryRepository_GetLibraryTvCardsAsync_ThrowsWhenCancelled()
    {
        LibraryRepository repository = new(contextFactory: _factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetLibraryTvCardsAsync(
                userId: SeedConstants.UserId,
                libraryId: SeedConstants.TvLibraryId,
                country: "US",
                take: 10,
                skip: 0,
                ct: cts.Token
            )
        );
    }

    [Fact]
    public async Task CollectionRepository_GetCollectionsListAsync_ThrowsWhenCancelled()
    {
        CollectionRepository repository = new(contextFactory: _factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetCollectionsListAsync(userId: SeedConstants.UserId, language: "en", country: "US", take: 10, page: 0, ct: cts.Token)
        );
    }

    [Fact]
    public async Task GenreRepository_GetGenresWithCountsAsync_ThrowsWhenCancelled()
    {
        GenreRepository repository = new(context: _context);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetGenresWithCountsAsync(userId: SeedConstants.UserId, language: "en", take: 10, page: 0, ct: cts.Token)
        );
    }

    [Fact]
    public async Task SpecialRepository_GetSpecialsAsync_ThrowsWhenCancelled()
    {
        SpecialRepository repository = new(context: _context, contextFactory: _factory);
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            repository.GetSpecialsAsync(userId: SeedConstants.UserId, language: "en", take: 10, page: 0, ct: cts.Token)
        );
    }

    [Fact]
    public async Task MovieRepository_GetMovieAsync_WorksWithDefaultToken()
    {
        MovieRepository repository = new(contextFactory: _factory, logger: NullLogger<MovieRepository>.Instance);

        Movie? movie = await repository.GetMovieAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US");

        Assert.NotNull(@object: movie);
        Assert.Equal(expected: "Spirited Away", actual: movie.Title);
    }

    [Fact]
    public async Task TvShowRepository_GetTvAvailableAsync_WorksWithDefaultToken()
    {
        TvShowRepository repository = new(contextFactory: _factory);

        bool available = await repository.GetTvAvailableAsync(userId: SeedConstants.UserId, id: 1399);

        Assert.True(condition: available);
    }

    public void Dispose()
    {
        _context.Dispose();
        _factoryConnection.Dispose();
    }
}
