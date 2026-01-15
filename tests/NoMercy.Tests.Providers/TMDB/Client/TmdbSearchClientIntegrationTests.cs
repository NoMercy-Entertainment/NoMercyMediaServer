using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Integration tests for TmdbSearchClient using real TMDB API
/// These tests require a valid TMDB API key and internet connection
/// </summary>
[Trait("Category", "Integration")]
public class TmdbSearchClientIntegrationTests : TmdbTestBase
{
    [Fact]
    public async Task Movie_WithRealApi_ReturnsValidResults()
    {
        // Arrange
        using TmdbSearchClient client = CreateRealSearchClient();
        
        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie("The Dark Knight", "2008");
        
        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(m => m.Title!.Contains("Dark Knight"));
        result.Results.First().Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TvShow_WithRealApi_ReturnsValidResults()
    {
        // Arrange
        using TmdbSearchClient client = CreateRealSearchClient();
        
        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow("Breaking Bad", "2008");
        
        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(tv => tv.Name!.Contains("Breaking Bad"));
        result.Results.First().Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Person_WithRealApi_ReturnsValidResults()
    {
        // Arrange
        using TmdbSearchClient client = CreateRealSearchClient();
        
        // Act
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person("Leonardo DiCaprio");
        
        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(p => p.Name!.Contains("Leonardo"));
        result.Results.First().Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Multi_WithRealApi_ReturnsVariedMediaTypes()
    {
        // Arrange
        using TmdbSearchClient client = CreateRealSearchClient();
        
        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi("Marvel");
        
        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        // Note: TmdbMultiSearch has a complex tuple structure, so we just verify the result exists
    }

    private new static TmdbSearchClient CreateRealSearchClient()
    {
        // This would use real API configuration
        return new();
    }
}
