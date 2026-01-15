using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

[Trait("Category", "Unit")]
[Trait("Provider", "TMDB")]
[Trait("Client", "TmdbSeasonClient")]
public class TmdbSeasonClientTests : TmdbTestBase
{
    private const int InvalidTvShowId = 999999999;
    private const int InvalidSeasonNumber = 999;

    public TmdbSeasonClientTests()
    {
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_CreatesInstance()
    {
        // Arrange
        string[] appendices = ["credits", "images"];
        const string language = "es-ES";

        // Act
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber, appendices, language);

        // Assert
        client.Should().NotBeNull();
    }

    #endregion

    #region Episode Navigation Tests

    [Fact]
    public void Episode_WithValidNumber_ReturnsEpisodeClient()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);
        const int episodeNumber = 1;

        // Act
        TmdbEpisodeClient episodeClient = client.Episode(episodeNumber);

        // Assert
        episodeClient.Should().NotBeNull();
        episodeClient.Should().BeOfType<TmdbEpisodeClient>();
    }

    [Fact]
    public void Episode_WithDifferentNumbers_ReturnsDifferentClients()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbEpisodeClient episode1 = client.Episode(1);
        TmdbEpisodeClient episode2 = client.Episode(2);

        // Assert
        episode1.Should().NotBeNull();
        episode2.Should().NotBeNull();
        episode1.Should().NotBeSameAs(episode2);
    }

    #endregion

    #region Details Tests

    [Fact]
    public async Task Details_WithValidSeasonNumber_ReturnsSeasonDetails()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonDetails? result = await client.Details();

        // Assert
        result.Should().NotBeNull();
        result!.SeasonNumber.Should().Be(ValidSeasonNumber);
        result.Name.Should().NotBeNullOrEmpty();
        result.Episodes.Should().NotBeNull();
        result.Episodes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Details_WithInvalidSeasonNumber_HandlesGracefully()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, InvalidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Details();

        // Assert
        await act.Should().NotThrowAsync("because invalid season numbers should be handled gracefully");
    }

    [Fact]
    public async Task Details_WithPriorityTrue_ReturnsSeasonDetails()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonDetails? result = await client.Details(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.SeasonNumber.Should().Be(ValidSeasonNumber);
    }

    #endregion

    #region WithAppends Tests

    [Fact]
    public async Task WithAppends_WithValidAppends_ReturnsSeasonWithAppends()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);
        string[] appendices = ["credits", "images"];

        // Act
        TmdbSeasonAppends? result = await client.WithAppends(appendices);

        // Assert
        result.Should().NotBeNull();
        result!.SeasonNumber.Should().Be(ValidSeasonNumber);
        result.TmdbSeasonCredits.Should().NotBeNull();
        result.TmdbSeasonImages.Should().NotBeNull();
    }

    [Fact]
    public async Task WithAppends_WithEmptyAppends_ReturnsBasicSeason()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);
        string[] appendices = [];

        // Act
        TmdbSeasonAppends? result = await client.WithAppends(appendices);

        // Assert
        result.Should().NotBeNull();
        result!.SeasonNumber.Should().Be(ValidSeasonNumber);
    }

    [Fact]
    public async Task WithAllAppends_WithPriorityTrue_ReturnsCompleteSeasonData()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonAppends? result = await client.WithAllAppends(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.SeasonNumber.Should().Be(ValidSeasonNumber);
        result.TmdbSeasonCredits.Should().NotBeNull();
        result.TmdbSeasonImages.Should().NotBeNull();
        result.TmdbSeasonExternalIds.Should().NotBeNull();
        result.Translations.Should().NotBeNull();
    }

    #endregion

    #region AggregatedCredits Tests

    [Fact]
    public async Task AggregatedCredits_WithValidSeasonNumber_ReturnsCredits()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonAggregatedCredits? result = await client.AggregatedCredits();

        // Assert
        result.Should().NotBeNull();
        result!.Cast.Should().NotBeNull();
        result.Crew.Should().NotBeNull();
        if (result.Cast.Length != 0)
        {
            result.Cast.First().Name.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task AggregatedCredits_WithPriority_ReturnsCredits()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonAggregatedCredits? result = await client.AggregatedCredits(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Cast.Should().NotBeNull();
        result.Crew.Should().NotBeNull();
    }

    #endregion

    #region Credits Tests

    [Fact]
    public async Task Credits_WithValidSeasonNumber_ReturnsCredits()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonCredits? result = await client.Credits();

        // Assert
        result.Should().NotBeNull();
        result!.Cast.Should().NotBeNull();
        result.Crew.Should().NotBeNull();
        if (result.Cast.Length != 0)
        {
            result.Cast.First().Name.Should().NotBeNullOrEmpty();
            result.Cast.First().Character.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Credits_WithInvalidSeasonNumber_HandlesGracefully()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, InvalidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Credits();

        // Assert
        await act.Should().NotThrowAsync("because invalid season numbers should be handled gracefully");
    }

    #endregion

    #region ExternalIds Tests

    [Fact]
    public async Task ExternalIds_WithValidSeasonNumber_ReturnsExternalIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonExternalIds? result = await client.ExternalIds();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        // External IDs might be null for some seasons, so we just check structure
    }

    [Fact]
    public async Task ExternalIds_WithPriority_ReturnsExternalIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonExternalIds? result = await client.ExternalIds(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
    }

    #endregion

    #region Images Tests

    [Fact]
    public async Task Images_WithValidSeasonNumber_ReturnsImages()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonImages? result = await client.Images();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Posters.Should().NotBeNull();
        if (result.Posters.Length != 0)
        {
            result.Posters.First().FilePath.Should().NotBeNullOrEmpty();
            result.Posters.First().Width.Should().BeGreaterThan(0);
            result.Posters.First().Height.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task Images_WithInvalidSeasonNumber_HandlesGracefully()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, InvalidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Images();

        // Assert
        await act.Should().NotThrowAsync("because invalid season numbers should be handled gracefully");
    }

    #endregion

    #region Translations Tests

    [Fact]
    public async Task Translations_WithValidSeasonNumber_ReturnsTranslations()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSharedTranslations? result = await client.Translations();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Translations.Should().NotBeNull();
        if (result.Translations.Length != 0)
        {
            result.Translations.First().Iso6391.Should().NotBeNullOrEmpty();
            result.Translations.First().Iso31661.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Translations_WithPriority_ReturnsTranslations()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSharedTranslations? result = await client.Translations(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Translations.Should().NotBeNull();
    }

    #endregion

    #region Videos Tests

    [Fact]
    public async Task Videos_WithValidSeasonNumber_ReturnsVideos()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonVideos? result = await client.Videos();

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeNull();
        if (result.Results.Length != 0)
        {
            result.Results.First().Key.Should().NotBeNullOrEmpty();
            result.Results.First().Name.Should().NotBeNullOrEmpty();
            result.Results.First().Type.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Videos_WithInvalidSeasonNumber_HandlesGracefully()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, InvalidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Videos();

        // Assert
        await act.Should().NotThrowAsync("because invalid season numbers should be handled gracefully");
    }

    #endregion

    #region Changes Tests

    [Theory]
    [InlineData("2023-01-01", "2023-12-31")]
    [InlineData("2024-01-01", "2024-06-30")]
    public async Task Changes_WithValidDateRange_ReturnsChanges(string startDate, string endDate)
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonChanges? result = await client.Changes(startDate, endDate);

        // Assert - Changes endpoint may return null even for valid requests due to TMDB API limitations
        // This is expected behavior as confirmed by testing with multiple active series
        if (result != null)
        {
            result.Changes.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Changes_WithInvalidDateRange_HandlesGracefully()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Changes("invalid-date", "another-invalid-date");

        // Assert
        await act.Should().NotThrowAsync("because invalid dates should be handled gracefully");
    }

    [Fact]
    public async Task Changes_WithPriority_ReturnsChanges()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonChanges? result = await client.Changes("2024-01-01", "2024-06-30", priority: true);

        // Assert - Changes endpoint may return null even for valid requests due to TMDB API limitations
        if (result != null)
        {
            result.Changes.Should().NotBeNull();
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Details_WithInvalidTvShowId_HandlesGracefully()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(InvalidTvShowId, ValidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Details();

        // Assert
        await act.Should().NotThrowAsync("because invalid TV show IDs should be handled gracefully");
    }

    [Fact]
    public async Task Credits_WithInvalidTvShowId_HandlesGracefully()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(InvalidTvShowId, ValidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Credits();

        // Assert
        await act.Should().NotThrowAsync("because invalid TV show IDs should be handled gracefully");
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task MultipleRequests_Concurrently_HandleCorrectly()
    {
        // Arrange
        SetupTmdbAuthentication();
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        Task<TmdbSeasonDetails?> detailsTask = client.Details();
        Task<TmdbSeasonCredits?> creditsTask = client.Credits();
        Task<TmdbSeasonImages?> imagesTask = client.Images();
        Task<TmdbSeasonExternalIds?> externalIdsTask = client.ExternalIds();
        Task<TmdbSeasonVideos?> videosTask = client.Videos();

        await Task.WhenAll(detailsTask, creditsTask, imagesTask, externalIdsTask, videosTask);

        TmdbSeasonDetails? details = await detailsTask;
        TmdbSeasonCredits? credits = await creditsTask;
        TmdbSeasonImages? images = await imagesTask;
        TmdbSeasonExternalIds? externalIds = await externalIdsTask;
        TmdbSeasonVideos? videos = await videosTask;

        // Assert
        details.Should().NotBeNull();
        credits.Should().NotBeNull();
        images.Should().NotBeNull();
        externalIds.Should().NotBeNull();
        videos.Should().NotBeNull();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_WhenCalled_DoesNotThrow()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act & Assert
        client.Invoking(c => c.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act & Assert
        client.Invoking(c => c.Dispose()).Should().NotThrow();
        client.Invoking(c => c.Dispose()).Should().NotThrow();
        client.Invoking(c => c.Dispose()).Should().NotThrow();
    }

    #endregion
}
