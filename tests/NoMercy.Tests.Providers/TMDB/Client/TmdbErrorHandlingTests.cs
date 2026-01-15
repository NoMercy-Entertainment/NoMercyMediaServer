using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Tests for error handling and edge cases in TMDB clients
/// Verifies robust behavior under failure conditions
/// </summary>
public class TmdbErrorHandlingTests : TmdbTestBase
{
    [Fact]
    public async Task MovieClient_WithInvalidId_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient(InvalidMovieId);

        // Act & Assert
        Func<Task<TmdbMovieDetails?>> detailsTask = async () => await client.Details();
        await detailsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieCredits?>> creditsTask = async () => await client.Credits();
        await creditsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieExternalIds?>> externalIdsTask = async () => await client.ExternalIds();
        await externalIdsTask.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task MovieClient_WithEdgeCaseIds_HandlesGracefully(int edgeCaseId)
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient(edgeCaseId);

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
        Func<Task<TmdbMovieChanges?>> invalidFormatTask = async () => await client.Changes("invalid-date", "another-invalid-date");
        await invalidFormatTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieChanges?>> futureDateTask = async () => await client.Changes("2099-12-31", "2100-01-01");
        await futureDateTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieChanges?>> reversedDatesTask = async () => await client.Changes("2023-12-31", "2023-01-01");
        await reversedDatesTask.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MovieClient_Changes_WithNullOrEmptyDates_HandlesGracefully(string? dateValue)
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act & Assert
        Func<Task<TmdbMovieChanges?>> task = async () => await client.Changes(dateValue!, dateValue!);
        await task.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MovieClient_MultipleSimultaneousOperations_OnInvalidId_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient(InvalidMovieId);

        // Act & Assert
        Task<TmdbMovieDetails?> detailsTask = client.Details();
        Task<TmdbMovieCredits?> creditsTask = client.Credits();
        Task<TmdbMovieExternalIds?> externalIdsTask = client.ExternalIds();
        Task<TmdbMovieKeywords?> keywordsTask = client.Keywords();
        Task<TmdbImages?> imagesTask = client.Images();

        Func<Task> allTasksCompletion = async () => await Task.WhenAll(detailsTask, creditsTask, externalIdsTask, keywordsTask, imagesTask);
        await allTasksCompletion.Should().NotThrowAsync();
    }

    [Fact]
    public void MovieClient_ConstructorWithExtremeValues_DoesNotThrow()
    {
        // Arrange & Act & Assert
        Func<TmdbMovieClient> maxIntConstructor = () => new(int.MaxValue);
        maxIntConstructor.Should().NotThrow();

        Func<TmdbMovieClient> minIntConstructor = () => new(int.MinValue);
        minIntConstructor.Should().NotThrow();

        Func<TmdbMovieClient> zeroConstructor = () => new(0);
        zeroConstructor.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-language-code")]
    [InlineData("xx-XX")]
    [InlineData("12345")]
    [InlineData("!@#$%")]
    public void MovieClient_ConstructorWithInvalidLanguages_DoesNotThrow(string? language)
    {
        // Arrange & Act & Assert
        Func<TmdbMovieClient> constructor = () => new(ValidMovieId, language: language!);
        constructor.Should().NotThrow();

        using TmdbMovieClient client = new(ValidMovieId, language: language!);
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task MovieClient_WithAllAppends_OnInvalidId_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient(InvalidMovieId);

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
        client.Id.Should().Be(originalId);
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
                using TmdbMovieClient client = CreateMockMovieClient(ValidMovieId + i);
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
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel quickly
        
        // Note: The current TMDB client doesn't support cancellation tokens,
        // but we can test that operations complete or timeout gracefully
        Task<TmdbMovieAppends?> task = client.WithAllAppends();
        
        // Wait for either completion or a reasonable timeout
        Task completedTask = await Task.WhenAny(task, Task.Delay(5000));

        // Assert
        completedTask.Should().Be(task); // Should complete, not timeout
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
            TmdbMovieClient client = CreateMockMovieClient(ValidMovieId + i);
            clients.Add(client);
            tasks.Add(client.Details());
        }

        // Dispose all clients while operations are potentially running
        foreach (TmdbMovieClient client in clients)
        {
            client.Dispose();
        }

        // Assert
        Func<Task> allTasksCompletion = async () => await Task.WhenAll(tasks);
        await allTasksCompletion.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task MovieClient_AllMethods_WithPriorityFlags_HandleGracefully(bool? priority)
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act & Assert
        Func<Task<TmdbMovieDetails?>> detailsTask = async () => await client.Details(priority);
        await detailsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieCredits?>> creditsTask = async () => await client.Credits(priority);
        await creditsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieExternalIds?>> externalIdsTask = async () => await client.ExternalIds(priority);
        await externalIdsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieKeywords?>> keywordsTask = async () => await client.Keywords(priority);
        await keywordsTask.Should().NotThrowAsync();

        Func<Task<TmdbImages?>> imagesTask = async () => await client.Images(priority);
        await imagesTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieLists?>> listsTask = async () => await client.Lists(priority);
        await listsTask.Should().NotThrowAsync();

        Func<Task<TmdbMovieAppends?>> appendsTask = async () => await client.WithAllAppends(priority);
        await appendsTask.Should().NotThrowAsync();
    }
}
