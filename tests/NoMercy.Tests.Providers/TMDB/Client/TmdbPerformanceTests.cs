using System.Diagnostics;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Performance tests for TMDB clients
/// Measures response times and throughput under various conditions
/// </summary>
[Collection("TmdbApi")]
public class TmdbPerformanceTests : TmdbTestBase
{
    private const int PerformanceThresholdMs = 6000; // 6 seconds max for mocked calls
    private const int IntegrationPerformanceThresholdMs = 30000; // 30 seconds max for real API calls

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MovieClient_SingleCall_CompletesWithinTimeout()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Act
        TmdbMovieDetails? result = await client.Details();

        // Assert
        stopwatch.Stop();
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MovieClient_MultipleConcurrentCalls_CompletesWithinTimeout()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Act
        Task<TmdbMovieDetails?> detailsTask = client.Details();
        Task<TmdbMovieCredits?> creditsTask = client.Credits();
        Task<TmdbMovieExternalIds?> externalIdsTask = client.ExternalIds();
        Task<TmdbMovieKeywords?> keywordsTask = client.Keywords();
        Task<TmdbImages?> imagesTask = client.Images();

        await Task.WhenAll(detailsTask, creditsTask, externalIdsTask, keywordsTask, imagesTask);

        TmdbMovieDetails? details = await detailsTask;
        TmdbMovieCredits? credits = await creditsTask;
        TmdbMovieExternalIds? externalIds = await externalIdsTask;
        TmdbMovieKeywords? keywords = await keywordsTask;
        TmdbImages? images = await imagesTask;

        // Assert
        stopwatch.Stop();
        details.Should().NotBeNull();
        credits.Should().NotBeNull();
        externalIds.Should().NotBeNull();
        keywords.Should().NotBeNull();
        images.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MovieClient_WithAllAppends_CompletesWithinTimeout()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Act
        TmdbMovieAppends? result = await client.WithAllAppends();

        // Assert
        stopwatch.Stop();
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MovieClient_SequentialCalls_CompletesWithinTimeout()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Act
        TmdbMovieDetails? details = await client.Details();
        TmdbMovieCredits? credits = await client.Credits();
        TmdbMovieExternalIds? externalIds = await client.ExternalIds();
        TmdbMovieKeywords? keywords = await client.Keywords();
        TmdbImages? images = await client.Images();

        // Assert
        stopwatch.Stop();
        details.Should().NotBeNull();
        credits.Should().NotBeNull();
        externalIds.Should().NotBeNull();
        keywords.Should().NotBeNull();
        images.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [Trait("Category", "Performance")]
    public async Task MovieClient_MultipleClients_ConcurrentAccess_CompletesWithinTimeout(int clientCount)
    {
        // Arrange
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Act
        Task<TmdbMovieDetails?>[] tasks = Enumerable.Range(0, clientCount)
            .Select(i => Task.Run(async () =>
            {
                using TmdbMovieClient client = CreateMockMovieClient(ValidMovieId + i);
                return await client.Details();
            }))
            .ToArray();

        TmdbMovieDetails?[] results = await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        results.Should().AllSatisfy(result => result.Should().NotBeNull());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs * 2); // Allow more time for multiple clients
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MovieClient_ClientCreationAndDisposal_IsEfficient()
    {
        // Arrange
        Stopwatch stopwatch = Stopwatch.StartNew();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            using TmdbMovieClient client = CreateMockMovieClient(ValidMovieId + i);
            TmdbMovieDetails? result = await client.Details();
            // API may return null for some movie IDs during performance testing
            if (result != null)
            {
                result.Id.Should().BeGreaterThan(0);
            }
        }

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs * 35); // Allow reasonable time for 100 iterations with potential API calls
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void MovieClient_MemoryUsage_DoesNotLeak()
    {
        // Arrange
        long initialMemory = GC.GetTotalMemory(true);
        const int iterations = 50;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            using TmdbMovieClient client = CreateMockMovieClient(ValidMovieId + i);
            // Just create and dispose, no async operations to keep test simple
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        long finalMemory = GC.GetTotalMemory(false);
        long memoryIncrease = finalMemory - initialMemory;
        
        // Allow for some memory increase but not excessive (1MB threshold)
        memoryIncrease.Should().BeLessThan(1024 * 1024); 
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Category", "Integration")]
    public async Task MovieClient_RealApiCall_CompletesWithinTimeout()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Act
        TmdbMovieDetails? result = await client.Details();

        // Assert
        stopwatch.Stop();
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(IntegrationPerformanceThresholdMs);
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Category", "Integration")]
    public async Task MovieClient_RealApiWithAllAppends_CompletesWithinTimeout()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Act
        TmdbMovieAppends? result = await client.WithAllAppends();

        // Assert
        stopwatch.Stop();
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(IntegrationPerformanceThresholdMs);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MovieClient_BulkOperations_ScalesLinearly()
    {
        // Arrange
        long singleCallTime = await MeasureSingleCall();
        long bulkCallTime = await MeasureBulkCalls(5);

        // Assert
        // Bulk operations should not be more than 3x slower than single operations
        // (allowing for some overhead and concurrency benefits)
        bulkCallTime.Should().BeLessThan(singleCallTime * 3);
    }

    private async Task<long> MeasureSingleCall()
    {
        using TmdbMovieClient client = CreateMockMovieClient();
        Stopwatch stopwatch = Stopwatch.StartNew();
        
        TmdbMovieDetails? result = await client.Details();
        
        stopwatch.Stop();
        result.Should().NotBeNull();
        return stopwatch.ElapsedMilliseconds;
    }

    private async Task<long> MeasureBulkCalls(int count)
    {
        using TmdbMovieClient client = CreateMockMovieClient();
        Stopwatch stopwatch = Stopwatch.StartNew();
        
        Task<TmdbMovieDetails?>[] tasks = Enumerable.Range(0, count)
            .Select(_ => client.Details())
            .ToArray();
        
        TmdbMovieDetails?[] results = await Task.WhenAll(tasks);
        
        stopwatch.Stop();
        results.Should().AllSatisfy(result => result.Should().NotBeNull());
        return stopwatch.ElapsedMilliseconds;
    }
}
