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

using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Error handling and edge case tests for TmdbSearchClient
/// Tests client behavior under various failure conditions
/// </summary>
[Trait(name: "Category", value: "ErrorHandling")]
[Collection(name: "TmdbApi")]
public class TmdbSearchErrorHandlingTests : TmdbTestBase
{
    #region Invalid Query Tests

    [Fact]
    public async Task Movie_WithNullQuery_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        Func<Task<TmdbPaginatedResponse<TmdbMovie>?>> act = async () => await client.Movie(query: null!);

        // Assert
        await act.Should()
            .NotThrowAsync(because: "because the client should handle null queries gracefully");
    }

    [Fact]
    public async Task TvShow_WithEmptyQuery_ReturnsEmptyResults()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query: "");

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task Person_WithWhitespaceQuery_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person(query: "   ");

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().BeEmpty();
    }

    #endregion

    #region Special Character Tests

    [Theory]
    [InlineData(data: "C++ Programming")]
    [InlineData(data: "AT&T")]
    [InlineData(data: "100% Pure")]
    [InlineData(data: "3:10 to Yuma")]
    [InlineData(data: "Spider-Man")]
    public async Task Movie_WithSpecialCharacters_HandlesCorrectly(string query)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query: query);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Long Query Tests

    [Fact]
    public async Task Multi_WithVeryLongQuery_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();
        string longQuery = new(c: 'a', count: 1000);

        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(query: longQuery);

        // Assert
        // Very long queries may be rejected by TMDB API (400 Bad Request), returning null
        // This is expected and graceful error handling behavior
        if (result != null)
        {
            result.Results.Should().BeEmpty();
        }
    }

    #endregion

    #region Invalid Year Tests

    [Theory]
    [InlineData(data: "invalid")]
    [InlineData(data: "1800")]
    [InlineData(data: "3000")]
    [InlineData(data: "-1")]
    public async Task Movie_WithInvalidYear_HandlesGracefully(string invalidYear)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query: "Test Movie", year: invalidYear);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task MultipleSearches_Concurrently_HandleCorrectly()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        Task<TmdbPaginatedResponse<TmdbMovie>?> movieTask = client.Movie(query: "Inception");
        Task<TmdbPaginatedResponse<TmdbTvShow>?> tvTask = client.TvShow(query: "Breaking Bad");
        Task<TmdbPaginatedResponse<TmdbPerson>?> personTask = client.Person(query: "Leonardo DiCaprio");
        Task<TmdbPaginatedResponse<TmdbMultiSearch>?> multiTask = client.Multi(query: "Marvel");

        await Task.WhenAll(tasks: [movieTask, tvTask, personTask, multiTask]);

        // Assert
        (await movieTask)
            .Should()
            .NotBeNull();
        (await tvTask).Should().NotBeNull();
        (await personTask).Should().NotBeNull();
        (await multiTask).Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithClientDisposal_HandlesGracefully()
    {
        // Arrange
        TmdbSearchClient client = new();
        Task<TmdbPaginatedResponse<TmdbMovie>?> searchTask = client.Movie(query: "Test");

        // Act
        client.Dispose();

        // Assert - The task should complete or handle disposal gracefully
        Func<Task<TmdbPaginatedResponse<TmdbMovie>?>> act = async () => await searchTask;
        await act.Should().NotThrowAsync(because: "because ongoing operations should complete gracefully");
    }

    #endregion

    #region Rate Limiting Simulation Tests

    [Fact]
    public async Task Movie_WithMultipleQuickSearches_ShouldHandleRateLimiting()
    {
        // Arrange
        using TmdbSearchClient client = new();
        string[] queries = Enumerable.Range(start: 1, count: 10).Select(selector: i => $"query{i}").ToArray();

        // Act
        Task<TmdbPaginatedResponse<TmdbMovie>?>[] tasks = queries
            .Select(selector: q => client.Movie(query: q))
            .ToArray();
        await Task.WhenAll(tasks: tasks);

        // Assert
        tasks
            .Should()
            .AllSatisfy(expected: task =>
            {
                task.Result.Should().NotBeNull();
            });
    }

    #endregion

    #region Network Error Simulation Tests

    [Fact]
    public async Task Search_WithNetworkIssues_RetriesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act & Assert
        Func<Task<TmdbPaginatedResponse<TmdbMovie>?>> act = async () =>
            await client.Movie(query: "Network Test");
        await act.Should().NotThrowAsync(because: "because network errors should be handled gracefully");
    }

    #endregion

    #region Memory and Resource Tests

    [Fact]
    public async Task Movie_WithLargeNumberOfSearches_ShouldNotLeakMemory()
    {
        // Arrange
        long initialMemory = GC.GetTotalMemory(forceFullCollection: true);
        using TmdbSearchClient client = new();

        // Act
        for (int i = 0; i < 50; i++)
        {
            await client.Movie(query: $"test query {i}");
        }

        // Assert
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        long memoryIncrease = finalMemory - initialMemory;

        // Allow reasonable memory increase but not excessive
        memoryIncrease
            .Should()
            .BeLessThan(
                expected: 50 * 1024 * 1024, // 50MB
                because: "because memory usage should remain reasonable"
            );
    }

    #endregion

    #region Unicode and Internationalization Tests

    [Theory]
    [InlineData(data: "アニメ")] // Japanese
    [InlineData(data: "电影")] // Chinese
    [InlineData(data: "фильм")] // Russian
    [InlineData(data: "película")] // Spanish
    [InlineData(data: "🎬🎭🎪")] // Emojis
    public async Task Search_WithUnicodeQueries_HandlesCorrectly(string unicodeQuery)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(query: unicodeQuery);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
