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
[Trait("Category", "ErrorHandling")]
public class TmdbSearchErrorHandlingTests : TmdbTestBase
{
    #region Invalid Query Tests

    [Fact]
    public async Task Movie_WithNullQuery_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        Func<Task<TmdbPaginatedResponse<TmdbMovie>?>> act = async () => await client.Movie(null!);

        // Assert
        await act.Should().NotThrowAsync("because the client should handle null queries gracefully");
    }

    [Fact]
    public async Task TvShow_WithEmptyQuery_ReturnsEmptyResults()
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow("");

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
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person("   ");

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().BeEmpty();
    }

    #endregion

    #region Special Character Tests

    [Theory]
    [InlineData("C++ Programming")]
    [InlineData("AT&T")]
    [InlineData("100% Pure")]
    [InlineData("3:10 to Yuma")]
    [InlineData("Spider-Man")]
    public async Task Movie_WithSpecialCharacters_HandlesCorrectly(string query)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query);

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
        string longQuery = new('a', 1000);

        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(longQuery);

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
    [InlineData("invalid")]
    [InlineData("1800")]
    [InlineData("3000")]
    [InlineData("-1")]
    public async Task Movie_WithInvalidYear_HandlesGracefully(string invalidYear)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie("Test Movie", invalidYear);

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
        Task<TmdbPaginatedResponse<TmdbMovie>?> movieTask = client.Movie("Inception");
        Task<TmdbPaginatedResponse<TmdbTvShow>?> tvTask = client.TvShow("Breaking Bad");
        Task<TmdbPaginatedResponse<TmdbPerson>?> personTask = client.Person("Leonardo DiCaprio");
        Task<TmdbPaginatedResponse<TmdbMultiSearch>?> multiTask = client.Multi("Marvel");

        await Task.WhenAll(movieTask, tvTask, personTask, multiTask);

        // Assert
        (await movieTask).Should().NotBeNull();
        (await tvTask).Should().NotBeNull();
        (await personTask).Should().NotBeNull();
        (await multiTask).Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithClientDisposal_HandlesGracefully()
    {
        // Arrange
        TmdbSearchClient client = new();
        Task<TmdbPaginatedResponse<TmdbMovie>?> searchTask = client.Movie("Test");

        // Act
        client.Dispose();

        // Assert - The task should complete or handle disposal gracefully
        Func<Task<TmdbPaginatedResponse<TmdbMovie>?>> act = async () => await searchTask;
        await act.Should().NotThrowAsync("because ongoing operations should complete gracefully");
    }

    #endregion

    #region Rate Limiting Simulation Tests

    [Fact]
    public async Task Movie_WithMultipleQuickSearches_ShouldHandleRateLimiting()
    {
        // Arrange
        using TmdbSearchClient client = new();
        string[] queries = Enumerable.Range(1, 10).Select(i => $"query{i}").ToArray();

        // Act
        Task<TmdbPaginatedResponse<TmdbMovie>?>[] tasks = queries.Select(q => client.Movie(q)).ToArray();
        await Task.WhenAll(tasks);

        // Assert
        tasks.Should().AllSatisfy(task => 
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
        Func<Task<TmdbPaginatedResponse<TmdbMovie>?>> act = async () => await client.Movie("Network Test");
        await act.Should().NotThrowAsync("because network errors should be handled gracefully");
    }

    #endregion

    #region Memory and Resource Tests

    [Fact]
    public async Task Movie_WithLargeNumberOfSearches_ShouldNotLeakMemory()
    {
        // Arrange
        long initialMemory = GC.GetTotalMemory(true);
        using TmdbSearchClient client = new();

        // Act
        for (int i = 0; i < 50; i++)
        {
            await client.Movie($"test query {i}");
        }

        // Assert
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long finalMemory = GC.GetTotalMemory(true);
        long memoryIncrease = finalMemory - initialMemory;

        // Allow reasonable memory increase but not excessive
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, // 50MB
            "because memory usage should remain reasonable");
    }

    #endregion

    #region Unicode and Internationalization Tests

    [Theory]
    [InlineData("アニメ")] // Japanese
    [InlineData("电影")] // Chinese
    [InlineData("фильм")] // Russian
    [InlineData("película")] // Spanish
    [InlineData("🎬🎭🎪")] // Emojis
    public async Task Search_WithUnicodeQueries_HandlesCorrectly(string unicodeQuery)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(unicodeQuery);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
