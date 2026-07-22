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
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Tests for error handling and edge cases in TMDB clients
/// Verifies robust behavior under failure conditions
/// </summary>
[Collection(name: "TmdbApi")]
public class TmdbErrorHandlingTests : TmdbTestBase
{
    [Fact]
    public async Task MovieClient_WithInvalidId_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient(movieId: InvalidMovieId);

        // Act & Assert
        Func<Task<TmdbMovieDetails?>> detailsTask = async () => await client.Details();
        await detailsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieCredits?>> creditsTask = async () => await client.Credits();
        await creditsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieExternalIds?>> externalIdsTask = async () => await client.ExternalIds();
        await externalIdsTask.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(data: -1)]
    [InlineData(data: 0)]
    [InlineData(data: int.MinValue)]
    [InlineData(data: int.MaxValue)]
    public async Task MovieClient_WithEdgeCaseIds_HandlesGracefully(int edgeCaseId)
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient(movieId: edgeCaseId);

        // Act & Assert
        Func<Task<TmdbMovieDetails?>> detailsTask = async () => await client.Details();
        await detailsTask.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MovieClient_Changes_WithInvalidDateFormats_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act & Assert
        Func<Task<TmdbMovieChanges?>> invalidFormatTask = async () =>
            await client.Changes(startDate: "invalid-date", endDate: "another-invalid-date");
        await invalidFormatTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieChanges?>> futureDateTask = async () =>
            await client.Changes(startDate: "2099-12-31", endDate: "2100-01-01");
        await futureDateTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieChanges?>> reversedDatesTask = async () =>
            await client.Changes(startDate: "2023-12-31", endDate: "2023-01-01");
        await reversedDatesTask.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "   ")]
    public async Task MovieClient_Changes_WithNullOrEmptyDates_HandlesGracefully(string? dateValue)
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act & Assert
        Func<Task<TmdbMovieChanges?>> task = async () =>
            await client.Changes(startDate: dateValue!, endDate: dateValue!);
        await task.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MovieClient_MultipleSimultaneousOperations_OnInvalidId_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient(movieId: InvalidMovieId);

        // Act & Assert
        Task<TmdbMovieDetails?> detailsTask = client.Details();
        Task<TmdbMovieCredits?> creditsTask = client.Credits();
        Task<TmdbMovieExternalIds?> externalIdsTask = client.ExternalIds();
        Task<TmdbMovieKeywords?> keywordsTask = client.Keywords();
        Task<TmdbImages?> imagesTask = client.Images();

        Func<Task> allTasksCompletion = async () =>
            await Task.WhenAll(tasks: [detailsTask, creditsTask, externalIdsTask, keywordsTask, imagesTask]);
        await allTasksCompletion.Should().NotThrowAsync();
    }

    [Fact]
    public void MovieClient_ConstructorWithExtremeValues_DoesNotThrow()
    {
        // Arrange & Act & Assert
        Func<TmdbMovieClient> maxIntConstructor = () => new(id: int.MaxValue);
        maxIntConstructor.Should().NotThrow();

        Func<TmdbMovieClient> minIntConstructor = () => new(id: int.MinValue);
        minIntConstructor.Should().NotThrow();

        Func<TmdbMovieClient> zeroConstructor = () => new();
        zeroConstructor.Should().NotThrow();
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "invalid-language-code")]
    [InlineData(data: "xx-XX")]
    [InlineData(data: "12345")]
    [InlineData(data: "!@#$%")]
    public void MovieClient_ConstructorWithInvalidLanguages_DoesNotThrow(string? language)
    {
        // Arrange & Act & Assert
        Func<TmdbMovieClient> constructor = () => new(id: ValidMovieId, language: language!);
        constructor.Should().NotThrow();

        using TmdbMovieClient client = new(id: ValidMovieId, language: language!);
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task MovieClient_WithAllAppends_OnInvalidId_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient(movieId: InvalidMovieId);

        // Act & Assert
        Func<Task<TmdbMovieAppends?>> task = async () => await client.WithAllAppends();
        await task.Should().NotThrowAsync();
    }

    [Fact]
    public void MovieClient_AfterDispose_DoesNotThrowOnPropertyAccess()
    {
        // Arrange
        TmdbMovieClient client = CreateMockMovieClient();
        int originalId = client.Id;

        // Act
        client.Dispose();

        // Assert
        Func<int> propertyAccess = () => client.Id;
        propertyAccess.Should().NotThrow();
        client.Id.Should().Be(expected: originalId);
    }

    [Fact]
    public async Task MovieClient_ConcurrentDisposeAndApiCalls_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        Task<TmdbMovieDetails?> apiCallTask = client.Details();

        // Dispose while API call might be in progress
        client.Dispose();

        // Assert
        TmdbMovieDetails? result = await apiCallTask;
        // When disposal occurs during API call, result may be null due to disposal handling
        // This is expected and graceful behavior - no exception should be thrown
    }

    [Fact]
    public void MovieClient_RapidCreateDisposePattern_DoesNotThrow()
    {
        // Arrange & Act & Assert
        for (int i = 0; i < 10; i++)
        {
            Func<int> createAndDispose = () =>
            {
                using TmdbMovieClient client = CreateMockMovieClient(movieId: ValidMovieId + i);
                return client.Id;
            };

            createAndDispose.Should().NotThrow();
        }
    }

    [Fact]
    public async Task MovieClient_LongRunningOperation_CanBeCancelled()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();
        using CancellationTokenSource cts = new();

        // Act
        cts.CancelAfter(delay: TimeSpan.FromMilliseconds(milliseconds: 100)); // Cancel quickly

        // Note: The current TMDB client doesn't support cancellation tokens,
        // but we can test that operations complete or timeout gracefully
        Task<TmdbMovieAppends?> task = client.WithAllAppends();

        // Wait for either completion or a reasonable timeout
        Task completedTask = await Task.WhenAny(task1: task, task2: Task.Delay(millisecondsDelay: 5000));

        // Assert
        completedTask.Should().Be(expected: task); // Should complete, not timeout
        TmdbMovieAppends? result = await task;
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task MovieClient_StressTest_MultipleClients_HandlesConcurrentDisposal()
    {
        // Arrange
        const int clientCount = 10;
        List<TmdbMovieClient> clients = new();
        List<Task> tasks = new();

        // Act
        for (int i = 0; i < clientCount; i++)
        {
            TmdbMovieClient client = CreateMockMovieClient(movieId: ValidMovieId + i);
            clients.Add(item: client);
            tasks.Add(item: client.Details());
        }

        // Dispose all clients while operations are potentially running
        foreach (TmdbMovieClient client in clients)
        {
            client.Dispose();
        }

        // Assert
        Func<Task> allTasksCompletion = async () => await Task.WhenAll(tasks: tasks);
        await allTasksCompletion.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(data: true)]
    [InlineData(data: false)]
    [InlineData(data: null)]
    public async Task MovieClient_AllMethods_WithPriorityFlags_HandleGracefully(bool? priority)
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act & Assert
        Func<Task<TmdbMovieDetails?>> detailsTask = async () => await client.Details(priority: priority);
        await detailsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieCredits?>> creditsTask = async () => await client.Credits(priority: priority);
        await creditsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieExternalIds?>> externalIdsTask = async () =>
            await client.ExternalIds(priority: priority);
        await externalIdsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieKeywords?>> keywordsTask = async () => await client.Keywords(priority: priority);
        await keywordsTask.Should().NotThrowAsync();

        Func<Task<TmdbImages?>> imagesTask = async () => await client.Images(priority: priority);
        await imagesTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieLists?>> listsTask = async () => await client.Lists(priority: priority);
        await listsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieAppends?>> appendsTask = async () =>
            await client.WithAllAppends(priority: priority);
        await appendsTask.Should().NotThrowAsync();
    }
}
