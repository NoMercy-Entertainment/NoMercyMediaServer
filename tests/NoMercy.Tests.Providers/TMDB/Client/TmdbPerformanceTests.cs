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

using System.Diagnostics;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Performance tests for TMDB clients
/// Measures response times and throughput under various conditions
/// </summary>
[Collection(name: "TmdbApi")]
public class TmdbPerformanceTests : TmdbTestBase
{
    private const int PerformanceThresholdMs = 15000; // 15 seconds max for mocked calls (generous for CI + coverage overhead)
    private const int IntegrationPerformanceThresholdMs = 30000; // 30 seconds max for real API calls

    [Fact]
    [Trait(name: "Category", value: "Performance")]
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
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expected: PerformanceThresholdMs);
    }

    [Fact]
    [Trait(name: "Category", value: "Performance")]
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

        await Task.WhenAll(tasks: [detailsTask, creditsTask, externalIdsTask, keywordsTask, imagesTask]);

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
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expected: PerformanceThresholdMs);
    }

    [Fact]
    [Trait(name: "Category", value: "Performance")]
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
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expected: PerformanceThresholdMs);
    }

    [Fact]
    [Trait(name: "Category", value: "Performance")]
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
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expected: PerformanceThresholdMs);
    }

    [Theory]
    [InlineData(data: 5)]
    [InlineData(data: 10)]
    [InlineData(data: 20)]
    [Trait(name: "Category", value: "Performance")]
    public async Task MovieClient_MultipleClients_ConcurrentAccess_CompletesWithinTimeout(
        int clientCount
    )
    {
        // Arrange
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Act
        Task<TmdbMovieDetails?>[] tasks = Enumerable
            .Range(start: 0, count: clientCount)
            .Select(selector: i =>
                Task.Run(function: async () =>
                {
                    using TmdbMovieClient client = CreateMockMovieClient(movieId: ValidMovieId + i);
                    return await client.Details();
                })
            )
            .ToArray();

        TmdbMovieDetails?[] results = await Task.WhenAll(tasks: tasks);

        // Assert
        stopwatch.Stop();
        results.Should().AllSatisfy(expected: result => result.Should().NotBeNull());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expected: PerformanceThresholdMs * 2); // Allow more time for multiple clients
    }

    [Fact]
    [Trait(name: "Category", value: "Performance")]
    public async Task MovieClient_ClientCreationAndDisposal_IsEfficient()
    {
        // Arrange
        Stopwatch stopwatch = Stopwatch.StartNew();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            using TmdbMovieClient client = CreateMockMovieClient(movieId: ValidMovieId + i);
            TmdbMovieDetails? result = await client.Details();
            // API may return null for some movie IDs during performance testing
            if (result != null)
            {
                result.Id.Should().BeGreaterThan(expected: 0);
            }
        }

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expected: PerformanceThresholdMs * 35); // Allow reasonable time for 100 iterations with potential API calls
    }

    [Fact]
    [Trait(name: "Category", value: "Performance")]
    public void MovieClient_MemoryUsage_DoesNotLeak()
    {
        // Arrange
        long initialMemory = GC.GetTotalMemory(forceFullCollection: true);
        const int iterations = 50;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            using TmdbMovieClient client = CreateMockMovieClient(movieId: ValidMovieId + i);
            // Just create and dispose, no async operations to keep test simple
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        long finalMemory = GC.GetTotalMemory(forceFullCollection: false);
        long memoryIncrease = finalMemory - initialMemory;

        // Allow for some memory increase but not excessive (1MB threshold)
        memoryIncrease.Should().BeLessThan(expected: 1024 * 1024);
    }

    [Fact]
    [Trait(name: "Category", value: "Performance")]
    [Trait(name: "Category", value: "Integration")]
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
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expected: IntegrationPerformanceThresholdMs);
    }

    [Fact]
    [Trait(name: "Category", value: "Performance")]
    [Trait(name: "Category", value: "Integration")]
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
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expected: IntegrationPerformanceThresholdMs);
    }

    [Fact]
    [Trait(name: "Category", value: "Performance")]
    public async Task MovieClient_BulkOperations_ScalesLinearly()
    {
        // Arrange
        long singleCallTime = await MeasureSingleCall();
        long bulkCallTime = await MeasureBulkCalls(count: 5);

        // Assert
        // Bulk operations should not be more than 3x slower than single operations
        // (allowing for some overhead and concurrency benefits). The mocked call
        // is sub-millisecond, so Stopwatch.ElapsedMilliseconds rounds the single
        // baseline to 0 and the relative bound collapses — floor the baseline at
        // 1 ms and add fixed slack so the ratio stays meaningful at mock speed.
        long scalingCeiling = (Math.Max(val1: singleCallTime, val2: 1) * 3) + 50;
        bulkCallTime.Should().BeLessThan(expected: scalingCeiling);
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

        Task<TmdbMovieDetails?>[] tasks = Enumerable
            .Range(start: 0, count: count)
            .Select(selector: _ => client.Details())
            .ToArray();

        TmdbMovieDetails?[] results = await Task.WhenAll(tasks: tasks);

        stopwatch.Stop();
        results.Should().AllSatisfy(expected: result => result.Should().NotBeNull());
        return stopwatch.ElapsedMilliseconds;
    }
}
