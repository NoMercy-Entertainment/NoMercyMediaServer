using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
///     Integration tests for TmdbChangesClient - the global daily change lists used to keep
///     locally held metadata in sync. Hits the real TMDB API. limit:1 keeps each call to a
///     single page so the test stays cheap.
/// </summary>
[Trait("Category", "Integration")]
[Collection("TmdbApi")]
public class TmdbChangesClientIntegrationTests : TmdbTestBase
{
    [Fact]
    public async Task MovieChanges_WithRealApi_ReturnsChangedMovieIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbChangesClient client = new();

        // Act
        List<TmdbChangeListItem>? result = await client.MovieChanges(limit: 1);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result!.Should().AllSatisfy(change => change.Id.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task TvChanges_WithRealApi_ReturnsChangedShowIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbChangesClient client = new();

        // Act
        List<TmdbChangeListItem>? result = await client.TvChanges(limit: 1);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result!.Should().AllSatisfy(change => change.Id.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task PersonChanges_WithRealApi_ReturnsChangedPersonIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbChangesClient client = new();

        // Act
        List<TmdbChangeListItem>? result = await client.PersonChanges(limit: 1);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result!.Should().AllSatisfy(change => change.Id.Should().BeGreaterThan(0));
    }
}
