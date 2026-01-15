using NoMercy.Providers.TMDB.Client;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbCollectionClient
/// Tests movie collection details and related metadata
/// </summary>
[Trait("Category", "Unit")]
public class TmdbCollectionClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidId_SetsIdCorrectly()
    {
        // Arrange
        const int expectedId = ValidCollectionId;

        // Act
        using TmdbCollectionClient client = new(expectedId);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(expectedId);
    }

    #endregion

    // TODO: Implement remaining Collection client tests
    // - Details tests
    // - Images tests
    // - Translations tests
}
