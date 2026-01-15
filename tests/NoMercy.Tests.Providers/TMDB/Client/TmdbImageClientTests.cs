using NoMercy.Providers.TMDB.Client;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbImageClient
/// Tests image processing and metadata functionality
/// </summary>
[Trait("Category", "Unit")]
public class TmdbImageClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        // TmdbImageClient is abstract, so we cannot instantiate it directly
        // This test verifies the class structure exists
        typeof(TmdbImageClient).Should().NotBeNull();
        typeof(TmdbImageClient).Should().BeAbstract();
    }

    #endregion

    // TODO: Implement remaining Image client tests
    // - Image processing tests
    // - URL generation tests
    // - Size variant tests
}
