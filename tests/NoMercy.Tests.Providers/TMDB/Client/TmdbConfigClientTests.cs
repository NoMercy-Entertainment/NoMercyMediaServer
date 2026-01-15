using NoMercy.Providers.TMDB.Client;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbConfigClient
/// Tests TMDB configuration, image settings, and available regions
/// </summary>
[Trait("Category", "Unit")]
public class TmdbConfigClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithNoParameters_CreatesInstance()
    {
        // Act
        using TmdbConfigClient client = new();

        // Assert
        client.Should().NotBeNull();
    }

    #endregion

    // TODO: Implement remaining Config client tests
    // - Configuration tests
    // - Countries tests
    // - Jobs tests
    // - Languages tests
    // - Primary translations tests
    // - Timezones tests
}
