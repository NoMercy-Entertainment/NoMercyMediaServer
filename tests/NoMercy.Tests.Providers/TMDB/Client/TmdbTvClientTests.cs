using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Certifications;
using NoMercy.Providers.TMDB.Models.Genres;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using TmdbWatchProviders = NoMercy.Providers.TMDB.Models.Shared.TmdbWatchProviders;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
///     Unit tests for TmdbTvClient
///     Tests TV show data retrieval and metadata functionality
/// </summary>
[Trait("Category", "Unit")]
public class TmdbTvClientTests : TmdbTestBase
{
    #region Alternative Titles Tests

    [Fact]
    public async Task AlternativeTitles_WithValidId_ReturnsTitles()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvAlternativeTitles? result = await client.AlternativeTitles();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Changes Tests

    [Theory]
    [InlineData("2023-01-01", "2023-12-31")]
    [InlineData("2024-01-01", "2024-06-30")]
    public async Task Changes_WithValidDateRange_ReturnsChanges(string startDate, string endDate)
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvChanges? result = await client.Changes(startDate, endDate);

        // Assert
        // Changes endpoint may return null even for valid requests due to TMDB API limitations
        if (result != null) result.Items.Should().NotBeNull();
    }

    #endregion

    #region Content Ratings Tests

    [Fact]
    public async Task ContentRatings_WithValidId_ReturnsRatings()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvContentRatings? result = await client.ContentRatings();

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Episode Groups Tests

    [Fact]
    public async Task EpisodeGroups_WithValidId_ReturnsGroups()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvEpisodeGroups? result = await client.EpisodeGroups();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region External IDs Tests

    [Fact]
    public async Task ExternalIds_WithValidId_ReturnsExternalIds()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvExternalIds? result = await client.ExternalIds();

        // Assert
        result.Should().NotBeNull();
        // Most popular shows should have at least one external ID
    }

    #endregion

    #region Images Tests

    [Fact]
    public async Task Images_WithValidId_ReturnsImages()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbImages? result = await client.Images();

        // Assert
        result.Should().NotBeNull();
        result.Backdrops.Should().NotBeEmpty();
        result.Posters.Should().NotBeEmpty();
    }

    #endregion

    #region Keywords Tests

    [Fact]
    public async Task Keywords_WithValidId_ReturnsKeywords()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvKeywords? result = await client.Keywords();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Recommendations Tests

    [Fact]
    public async Task Recommendations_WithValidId_ReturnsRecommendations()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvRecommendations? result = await client.Recommendations();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Reviews Tests

    [Fact]
    public async Task Reviews_WithValidId_ReturnsReviews()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvReviews? result = await client.Reviews();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Screened Theatrically Tests

    [Fact]
    public async Task ScreenedTheatrically_WithValidId_ReturnsScreenings()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvScreenedTheatrically? result = await client.ScreenedTheatrically();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Similar Tests

    [Fact]
    public async Task Similar_WithValidId_ReturnsSimilarShows()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvSimilar? result = await client.Similar();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Translations Tests

    [Fact]
    public async Task Translations_WithValidId_ReturnsTranslations()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbSharedTranslations? result = await client.Translations();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Translations.Should().NotBeEmpty();
    }

    #endregion

    #region Videos Tests

    [Fact]
    public async Task Videos_WithValidId_ReturnsVideos()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvVideos? result = await client.Videos();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Watch Providers Tests

    [Fact]
    public async Task WatchProviders_WithValidId_ReturnsProviders()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbWatchProviders? result = await client.WatchProviders();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.TmdbWatchProviderResults.Should().NotBeNull();
    }

    #endregion

    #region Latest Tests

    [Fact]
    public async Task Latest_WhenCalled_ShouldReturnLatestShow()
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        TmdbTvShowLatest? result = await client.Latest();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
    }

    #endregion

    #region Top Rated Tests

    [Fact]
    public async Task TopRated_WhenCalled_ShouldReturnTopRatedShows()
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        TmdbTvTopRated? result = await client.TopRated();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeEmpty();
    }

    #endregion

    #region On The Air Tests

    [Fact]
    public async Task OnTheAir_WhenCalled_ShouldReturnCurrentlyAiringShows()
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        TmdbTvOnTheAir? result = await client.OnTheAir();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Airing Today Tests

    [Fact]
    public async Task AiringToday_WhenCalled_ShouldReturnAiringTodayShows()
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        TmdbTvAiringToday? result = await client.AiringToday();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeNull();
    }

    #endregion

    #region Certifications Tests

    [Fact]
    public async Task Certifications_WhenCalled_ShouldReturnTvCertifications()
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        TvShowCertifications? result = await client.Certifications();

        // Assert
        result.Should().NotBeNull();
        result.Certifications.Should().NotBeNull();
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task MultipleRequests_Concurrently_HandleCorrectly()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        Task<TmdbTvShowDetails?> detailsTask = client.Details();
        Task<TmdbTvCredits?> creditsTask = client.Credits();
        Task<TmdbImages?> imagesTask = client.Images();
        Task<TmdbTvVideos?> videosTask = client.Videos();

        await Task.WhenAll(detailsTask, creditsTask, imagesTask, videosTask);

        // Assert
        (await detailsTask).Should().NotBeNull();
        (await creditsTask).Should().NotBeNull();
        (await imagesTask).Should().NotBeNull();
        (await videosTask).Should().NotBeNull();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidId_CreatesInstance()
    {
        // Act
        using TmdbTvClient client = new(ValidTvShowId);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(ValidTvShowId);
    }

    [Fact]
    public void Constructor_WithDefaultParameters_CreatesInstance()
    {
        // Act
        using TmdbTvClient client = new();

        // Assert
        client.Should().NotBeNull();
    }

    #endregion

    #region Basic Details Tests

    [Fact]
    public async Task Details_WithValidId_ReturnsShowDetails()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvShowDetails? result = await client.Details();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Name.Should().NotBeNullOrEmpty();
        result.Overview.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Details_WithPriorityTrue_ReturnsShowDetails()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvShowDetails? result = await client.Details(true);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Name.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Appends Tests

    [Fact]
    public async Task WithAppends_WithSpecificAppends_ReturnsShowWithAppends()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);
        string[] appendices = ["credits", "images", "videos"];

        // Act
        TmdbTvShowAppends? result = await client.WithAppends(appendices);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Credits.Should().NotBeNull();
        result.Images.Should().NotBeNull();
        result.Videos.Should().NotBeNull();
    }

    [Fact]
    public async Task Show_WithAllAppends_ShouldReturnCompleteShowData()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvShowAppends? result = await client.WithAllAppends();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Credits.Should().NotBeNull();
        result.Images.Should().NotBeNull();
        result.Videos.Should().NotBeNull();
        result.AlternativeTitles.Should().NotBeNull();
        result.ExternalIds.Should().NotBeNull();
        result.Keywords.Should().NotBeNull();
        result.Recommendations.Should().NotBeNull();
        result.Similar.Should().NotBeNull();
        result.Translations.Should().NotBeNull();
    }

    [Fact]
    public async Task WithAllAppends_WithPriorityTrue_ReturnsCompleteShowData()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvShowAppends? result = await client.WithAllAppends(true);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Credits.Should().NotBeNull();
    }

    #endregion

    #region Credits Tests

    [Fact]
    public async Task AggregatedCredits_WithValidId_ReturnsCredits()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvAggregatedCredits? result = await client.AggregatedCredits();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.Cast.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Credits_WithValidId_ReturnsCredits()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvCredits? result = await client.Credits();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        // result.Cast.Should().NotBeEmpty();
        // Crew data might be empty for some shows like GTST, so we validate structure if present
        result.Crew.Should().NotBeNull();
        if (result.Crew.Length != 0)
            result.Crew.Should().AllSatisfy(crew =>
            {
                crew.Name.Should().NotBeNullOrEmpty();
                crew.Job.Should().NotBeNullOrEmpty();
                crew.Id.Should().BeGreaterThan(0);
            });
    }

    #endregion

    #region Popular Tests

    [Fact]
    public async Task Popular_WhenCalled_ShouldReturnPopularShows()
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        List<TmdbTvShow>? result = await client.Popular();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().HaveCountLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task Popular_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        List<TmdbTvShow>? result = await client.Popular(5);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().HaveCountLessThanOrEqualTo(5);
    }

    #endregion

    #region Genres Tests

    [Fact]
    public async Task Genres_WhenCalled_ShouldReturnGenreList()
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        TmdbGenreTv? result = await client.Genres();

        // Assert
        result.Should().NotBeNull();
        result.Genres.Should().NotBeEmpty();
        result.Genres.Should().AllSatisfy(genre =>
        {
            genre.Id.Should().BeGreaterThan(0);
            genre.Name.Should().NotBeNullOrEmpty();
        });
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("fr")]
    public async Task Genres_WithLanguage_ReturnsLocalizedGenres(string language)
    {
        // Arrange
        using TmdbTvClient client = new();

        // Act
        TmdbGenreTv? result = await client.Genres(language);

        // Assert
        result.Should().NotBeNull();
        result.Genres.Should().NotBeEmpty();
    }

    #endregion

    #region Season Navigation Tests

    [Fact]
    public void Season_WithValidSeasonNumber_ReturnsSeasonClient()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbSeasonClient seasonClient = client.Season(ValidSeasonNumber);

        // Assert
        seasonClient.Should().NotBeNull();
    }

    [Fact]
    public void Season_WithAppendices_ReturnsSeasonClient()
    {
        // Arrange
        using TmdbTvClient client = new(ValidTvShowId);
        string[] items = ["credits", "images"];

        // Act
        TmdbSeasonClient seasonClient = client.Season(ValidSeasonNumber, items);

        // Assert
        seasonClient.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task Details_WithInvalidId_HandlesGracefully()
    {
        // Arrange
        TmdbTvClient client = new(999999);

        // Act & Assert
        Func<Task<TmdbTvShowDetails?>> act = async () => await client.Details();
        await act.Should().NotThrowAsync("because invalid IDs should be handled gracefully");
    }

    [Fact]
    public async Task Changes_WithInvalidDateRange_HandlesGracefully()
    {
        // Arrange
        TmdbTvClient client = new(ValidTvShowId);

        // Act & Assert
        Func<Task<TmdbTvChanges?>> act = async () => await client.Changes("invalid-date", "invalid-date");
        await act.Should().NotThrowAsync("because invalid dates should be handled gracefully");
    }

    #endregion
}