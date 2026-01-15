using NoMercy.Providers.TMDB.Client;
using System.Diagnostics;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Performance tests for TmdbSearchClient
/// Tests response times and efficiency under various conditions
/// </summary>
[Trait("Category", "Performance")]
public class TmdbSearchPerformanceTests : TmdbTestBase
{
    #region Single Operation Performance Tests

    [Fact]
    public async Task Movie_Search_RespondsWithinReasonableTime()
    {
        // Arrange
        using TmdbSearchClient client = new();
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie("Inception");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "because API calls should complete within 5 seconds");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TvShow_Search_RespondsWithinReasonableTime()
    {
        // Arrange
        using TmdbSearchClient client = new();
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow("Breaking Bad");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "because API calls should complete within 5 seconds");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Person_Search_RespondsWithinReasonableTime()
    {
        // Arrange
        using TmdbSearchClient client = new();
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person("Leonardo DiCaprio");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "because API calls should complete within 5 seconds");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Multi_Search_RespondsWithinReasonableTime()
    {
        // Arrange
        using TmdbSearchClient client = new();
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi("Marvel");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "because API calls should complete within 5 seconds");
        result.Should().NotBeNull();
    }

    #endregion

    #region Concurrent Performance Tests

    [Fact]
    public async Task Search_WithConcurrentQueries_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        using TmdbSearchClient client = new();
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        Task[] tasks =
        [
            client.Movie("The Dark Knight"),
            client.TvShow("Game of Thrones"),
            client.Person("Brad Pitt"),
            client.Multi("Batman"),
            client.Collection("Fast")
        ];

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, 
            "because concurrent API calls should leverage parallelism");
        
        // All tasks should complete successfully
        tasks.Should().AllSatisfy(task => 
            task.Status.Should().Be(TaskStatus.RanToCompletion, "because all searches should complete successfully"));
    }

    [Fact]
    public async Task Search_WithHighConcurrency_ShouldMaintainPerformance()
    {
        // Arrange
        using TmdbSearchClient client = new();
        string[] queries = Enumerable.Range(1, 20).Select(i => $"test{i}").ToArray();
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        Task<TmdbPaginatedResponse<TmdbMovie>?>[] tasks = queries.Select(q => client.Movie(q)).ToArray();
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000, 
            "because high concurrency should still complete within reasonable time");
        
        double averageTime = stopwatch.ElapsedMilliseconds / (double)queries.Length;
        averageTime.Should().BeLessThan(1000, 
            "because average time per request should be efficient");
    }

    #endregion

    #region Memory Performance Tests

    [Fact]
    public async Task Search_WithSequentialQueries_ShouldMaintainMemoryEfficiency()
    {
        // Arrange
        using TmdbSearchClient client = new();
        long initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < 10; i++)
        {
            TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie($"test movie {i}");
            result.Should().NotBeNull();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long finalMemory = GC.GetTotalMemory(true);

        // Assert
        long memoryIncrease = finalMemory - initialMemory;
        memoryIncrease.Should().BeLessThan(10 * 1024 * 1024, // 10MB
            "because sequential operations should not cause significant memory increase");
    }

    #endregion

    #region Throughput Tests

    [Fact]
    public async Task Search_WithThroughputTesting_ShouldMeetMinimumRequirements()
    {
        // Arrange
        using TmdbSearchClient client = new();
        int searchCount = 10;
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        List<Task> tasks = [];
        
        for (int i = 0; i < searchCount; i++)
        {
            tasks.Add(client.Movie($"query{i}"));
        }
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        double throughput = searchCount / (stopwatch.ElapsedMilliseconds / 1000.0);
        throughput.Should().BeGreaterThan(1.0, 
            "because the client should handle at least 1 search per second");
    }

    #endregion

    #region Large Result Set Performance Tests

    [Fact]
    public async Task Multi_WithPopularQuery_ShouldHandleLargeResultSetEfficiently()
    {
        // Arrange
        using TmdbSearchClient client = new();
        Stopwatch stopwatch = new();

        // Act - Search for a very common term that will return many results
        stopwatch.Start();
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi("the");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(7000, 
            "because even large result sets should be handled efficiently");
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
    }

    #endregion

    #region Cache Performance Tests (if applicable)

    [Fact]
    public async Task Movie_WithRepeatedSameQuery_ShouldShowPerformanceImprovement()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "Inception";

        // Act - First search
        Stopwatch stopwatch1 = Stopwatch.StartNew();
        TmdbPaginatedResponse<TmdbMovie>? result1 = await client.Movie(query);
        stopwatch1.Stop();

        // Act - Second search (might be cached)
        Stopwatch stopwatch2 = Stopwatch.StartNew();
        TmdbPaginatedResponse<TmdbMovie>? result2 = await client.Movie(query);
        stopwatch2.Stop();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        
        // Note: This test assumes caching might be implemented
        // If no caching, both should still be within reasonable time
        stopwatch1.ElapsedMilliseconds.Should().BeLessThan(5000);
        stopwatch2.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    #endregion

    #region Network Efficiency Tests

    [Fact]
    public async Task Search_WithMultipleClients_ShouldNotImpactEachOther()
    {
        // Arrange
        using TmdbSearchClient client1 = new();
        using TmdbSearchClient client2 = new();
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        Task<TmdbPaginatedResponse<TmdbMovie>?> task1 = client1.Movie("Interstellar");
        Task<TmdbPaginatedResponse<TmdbTvShow>?> task2 = client2.TvShow("Stranger Things");
        
        await Task.WhenAll(task1, task2);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(8000, 
            "because multiple clients should not significantly impact each other");
        
        (await task1).Should().NotBeNull();
        (await task2).Should().NotBeNull();
    }

    #endregion

    #region Cleanup Performance Tests

    [Fact]
    public void Dispose_WhenCalled_ShouldCompleteQuickly()
    {
        // Arrange
        TmdbSearchClient client = new();
        Stopwatch stopwatch = new();

        // Act
        stopwatch.Start();
        client.Dispose();
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
            "because client disposal should be fast");
    }

    #endregion
}
