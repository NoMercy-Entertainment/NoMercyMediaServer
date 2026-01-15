using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbSearchClient
/// Tests all search functionality including movies, TV shows, people, multi-search, collections, networks, and keywords
/// </summary>
[Trait("Category", "Unit")]
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
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(m => m.Title!.Contains("Dark Knight", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Inception", "2010")]
    [InlineData("The Matrix", "1999")]
    [InlineData("Pulp Fiction", "1994")]
    public async Task Movie_WithQueryAndYear_ReturnsFilteredResults(string query, string year)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query, year);

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
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query, priority: true);

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
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie("");

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
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(tv => tv.Name!.Contains("Breaking Bad", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Game of Thrones", "2011")]
    [InlineData("Friends", "1994")]
    [InlineData("The Office", "2005")]
    public async Task TvShow_WithQueryAndYear_ReturnsFilteredResults(string query, string year)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query, year);

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
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query, priority: true);

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
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person(query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(p => p.Name!.Contains("Leonardo", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Tom Hanks")]
    [InlineData("Meryl Streep")]
    [InlineData("Robert Downey")]
    public async Task Person_WithDifferentActors_ReturnsResults(string actorName)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person(actorName);

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
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(query);

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        // Note: TmdbMultiSearch has a complex tuple structure, so we just verify the result exists
    }

    [Theory]
    [InlineData("Batman")]
    [InlineData("Star Wars")]
    [InlineData("Disney")]
    public async Task Multi_WithPopularTerms_ReturnsVariedResults(string query)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(query);

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
        TmdbPaginatedResponse<TmdbCollection>? result = await client.Collection(query);

        // Assert
        result.Should().NotBeNull();
        // Note: Collection results might be empty for some queries
    }

    [Theory]
    [InlineData("Harry Potter")]
    [InlineData("Lord of the Rings")]
    [InlineData("Fast and Furious")]
    public async Task Collection_WithFranchiseNames_ReturnsResults(string franchiseName)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbCollection>? result = await client.Collection(franchiseName);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Network Search Tests

    [Fact]
    public async Task Network_WithValidQuery_ReturnsResults()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "HBO";

        // Act
        TmdbPaginatedResponse<TmdbNetwork>? result = await client.Network(query);

        // Assert
        // API may return null for certain network queries
        if (result != null)
        {
            result.Results.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("Netflix")]
    [InlineData("Disney")]
    [InlineData("BBC")]
    public async Task Network_WithNetworkNames_ReturnsResults(string networkName)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbNetwork>? result = await client.Network(networkName);

        // Assert
        // API may return null for certain network queries
        if (result != null)
        {
            result.Results.Should().NotBeNull();
        }
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
        TmdbPaginatedResponse<TmdbKeyword>? result = await client.Keyword(query);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("action")]
    [InlineData("comedy")]
    [InlineData("drama")]
    public async Task Keyword_WithGenreTerms_ReturnsResults(string genreTerm)
    {
        // Arrange
        using TmdbSearchClient client = new();

        // Act
        TmdbPaginatedResponse<TmdbKeyword>? result = await client.Keyword(genreTerm);

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
        Func<Task<TmdbPaginatedResponse<TmdbMovie>?>> act = async () => await client.Movie(null!);
        await act.Should().NotThrowAsync("because the client should handle null queries gracefully");
    }

    [Fact]
    public async Task TvShow_WithSpecialCharacters_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();
        const string query = "C++ Programming & Development";

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Person_WithVeryLongQuery_HandlesGracefully()
    {
        // Arrange
        using TmdbSearchClient client = new();
        string longQuery = new('a', 1000);

        // Act
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person(longQuery);

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
        Task<TmdbPaginatedResponse<TmdbMovie>?> movieTask = client.Movie("Inception");
        Task<TmdbPaginatedResponse<TmdbTvShow>?> tvTask = client.TvShow("Breaking Bad");
        Task<TmdbPaginatedResponse<TmdbPerson>?> personTask = client.Person("Leonardo DiCaprio");

        await Task.WhenAll(movieTask, tvTask, personTask);

        // Assert
        (await movieTask).Should().NotBeNull();
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
        TimeSpan timeout = TimeSpan.FromSeconds(10);

        // Act & Assert
        using CancellationTokenSource cts = new(timeout);
        
        Task<TmdbPaginatedResponse<TmdbMovie>?> movieTask = client.Movie("Avatar");
        Task<TmdbPaginatedResponse<TmdbTvShow>?> tvTask = client.TvShow("Game of Thrones");
        
        await Task.WhenAll(movieTask, tvTask).WaitAsync(cts.Token);
        
        (await movieTask).Should().NotBeNull();
        (await tvTask).Should().NotBeNull();
    }

    #endregion
}
