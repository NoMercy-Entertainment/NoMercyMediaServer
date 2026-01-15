using NoMercy.Providers.TMDB.Client;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbPersonClient
/// Tests person details, credits, images, and related metadata
/// </summary>
[Trait("Category", "Unit")]
public class TmdbPersonClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidId_SetsIdCorrectly()
    {
        // Arrange
        const int expectedId = ValidPersonId;

        // Act
        using TmdbPersonClient client = new(expectedId);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(expectedId);
    }

    #endregion

    // TODO: Implement remaining Person client tests
    // - Details tests
    // - WithAllAppends tests
    // - Credits (movie/tv) tests
    // - Images, ExternalIds tests
    // - Combined credits tests
}
