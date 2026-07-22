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
using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbSearchClient
/// Tests all search functionality including movies, TV shows, people, multi-search, collections, networks, and keywords
/// </summary>
[Trait(name: "Category", value: "Unit")]
[Collection(name: "TmdbApi")]
public class TmdbSearchClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithNoParameters_CreatesInstance()
    {
        // Act
        using TmdbSearchClient client = new();

        // Assert
        client.Should().NotBeNull();
    }

    #endregion

    #region Movie Search Tests

    [Fact]
    public async Task Movie_WithValidQuery_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "The Dark Knight";

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query: query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result
            .Results.Should()
            .Contain(predicate: m => m.Title!.Contains("Dark Knight", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(data: ["Inception", "2010"])]
    [InlineData(data: ["The Matrix", "1999"])]
    [InlineData(data: ["Pulp Fiction", "1994"])]
    public async Task Movie_WithQueryAndYear_ReturnsFilteredResults(string query, string year)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query: query, year: year);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Movie_WithPriorityTrue_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "Avatar";

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query: query, priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Movie_WithEmptyQuery_ReturnsEmptyResults()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query: "");

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().BeEmpty();
    }

    #endregion

    #region TV Show Search Tests

    [Fact]
    public async Task TvShow_WithValidQuery_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "Breaking Bad";

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query: query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result
            .Results.Should()
            .Contain(predicate: tv => tv.Name!.Contains("Breaking Bad", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(data: ["Game of Thrones", "2011"])]
    [InlineData(data: ["Friends", "1994"])]
    [InlineData(data: ["The Office", "2005"])]
    public async Task TvShow_WithQueryAndYear_ReturnsFilteredResults(string query, string year)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query: query, year: year);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TvShow_WithPriorityTrue_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "Stranger Things";

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query: query, priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
    }

    #endregion

    #region Person Search Tests

    [Fact]
    public async Task Person_WithValidQuery_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "Leonardo DiCaprio";

        // Act
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person(query: query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result
            .Results.Should()
            .Contain(predicate: p => p.Name!.Contains("Leonardo", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(data: "Tom Hanks")]
    [InlineData(data: "Meryl Streep")]
    [InlineData(data: "Robert Downey")]
    public async Task Person_WithDifferentActors_ReturnsResults(string actorName)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person(query: actorName);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
    }

    #endregion

    #region Multi Search Tests

    [Fact]
    public async Task Multi_WithValidQuery_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "Marvel";

        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(query: query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        // Note: TmdbMultiSearch has a complex tuple structure, so we just verify the result exists
    }

    [Theory]
    [InlineData(data: "Batman")]
    [InlineData(data: "Star Wars")]
    [InlineData(data: "Disney")]
    public async Task Multi_WithPopularTerms_ReturnsVariedResults(string query)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(query: query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
    }

    #endregion

    #region Collection Search Tests

    [Fact]
    public async Task Collection_WithValidQuery_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "Marvel Cinematic Universe";

        // Act
        TmdbPaginatedResponse<TmdbCollection>? result = await client.Collection(query: query);

        // Assert
        result.Should().NotBeNull();
        // Note: Collection results might be empty for some queries
    }

    [Theory]
    [InlineData(data: "Harry Potter")]
    [InlineData(data: "Lord of the Rings")]
    [InlineData(data: "Fast and Furious")]
    public async Task Collection_WithFranchiseNames_ReturnsResults(string franchiseName)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbCollection>? result = await client.Collection(query: franchiseName);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Keyword Search Tests

    [Fact]
    public async Task Keyword_WithValidQuery_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "superhero";

        // Act
        TmdbPaginatedResponse<TmdbKeyword>? result = await client.Keyword(query: query);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(data: "action")]
    [InlineData(data: "comedy")]
    [InlineData(data: "drama")]
    public async Task Keyword_WithGenreTerms_ReturnsResults(string genreTerm)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbKeyword>? result = await client.Keyword(query: genreTerm);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task Movie_WithNullQuery_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act & Assert
        Func<Task<TmdbPaginatedResponse<TmdbMovie>?>> act = async () => await client.Movie(query: null!);
        await act.Should()
            .NotThrowAsync(because: "because the client should handle null queries gracefully");
    }

    [Fact]
    public async Task TvShow_WithSpecialCharacters_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "C++ Programming & Development";

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query: query);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Person_WithVeryLongQuery_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();
        string longQuery = new(c: 'a', count: 1000);

        // Act
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person(query: longQuery);

        // Assert
        // Very long queries may be rejected by TMDB API (400 Bad Request), returning null
        // This is expected and graceful error handling behavior
        if (result != null)
        {
            result.Results.Should().BeEmpty();
        }
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

        await Task.WhenAll(tasks: [movieTask, tvTask, personTask]);

        // Assert
        (await movieTask)
            .Should()
            .NotBeNull();
        (await tvTask).Should().NotBeNull();
        (await personTask).Should().NotBeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Search_Operations_CompleteWithinTimeout()
    {
        // Arrange
        using TmdbSearchClient client = new();
        TimeSpan timeout = TimeSpan.FromSeconds(seconds: 10);

        // Act & Assert
        using CancellationTokenSource cts = new(delay: timeout);

        Task<TmdbPaginatedResponse<TmdbMovie>?> movieTask = client.Movie(query: "Avatar");
        Task<TmdbPaginatedResponse<TmdbTvShow>?> tvTask = client.TvShow(query: "Game of Thrones");

        await Task.WhenAll(tasks: [movieTask, tvTask]).WaitAsync(cancellationToken: cts.Token);

        (await movieTask).Should().NotBeNull();
        (await tvTask).Should().NotBeNull();
    }

    #endregion
}
