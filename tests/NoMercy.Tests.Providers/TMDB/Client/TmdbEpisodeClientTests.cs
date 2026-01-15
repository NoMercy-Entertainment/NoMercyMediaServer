using NoMercy.Providers.TMDB.Client;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbEpisodeClient
/// Tests episode details, credits, images, and related metadata
/// </summary>
[Trait("Category", "Unit")]
public class TmdbEpisodeClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_SetsValuesCorrectly()
    {
        // Arrange
        const int tvId = ValidTvShowId;
        const int seasonNumber = 1;
        const int episodeNumber = 1;

        // Act
        using TmdbEpisodeClient client = new(tvId, seasonNumber, episodeNumber);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(tvId);
    }

    #endregion

    // TODO: Implement remaining Episode client tests
    // - Details tests
    // - WithAllAppends tests
    // - Credits, Images, Videos tests
    // - ExternalIds, Translations tests
}
