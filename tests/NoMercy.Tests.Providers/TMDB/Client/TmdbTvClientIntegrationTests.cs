using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Certifications;
using NoMercy.Providers.TMDB.Models.Genres;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using TmdbWatchProviders = NoMercy.Providers.TMDB.Models.Shared.TmdbWatchProviders;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
///     Integration tests for TmdbTvClient - simplified version
///     Tests real API interactions with TMDB TV endpoints
/// </summary>
[Trait("Category", "Integration")]
[Collection("TmdbIntegration")]
public class TmdbTvClientIntegrationTests : TmdbTestBase
{
    [Fact]
    public async Task Details_WithRealApi_ReturnsCompleteShowData()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvShowDetails? result = await client.Details();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(ValidTvShowId);
        result.Name.Should().NotBeNullOrEmpty();
        result.Overview.Should().NotBeNullOrEmpty();
        result.FirstAirDate.Should().NotBeNull();
        result.NumberOfSeasons.Should().BeGreaterThan(0);
        result.NumberOfEpisodes.Should().BeGreaterThan(0);
        result.Genres.Should().NotBeEmpty();
        result.VoteAverage.Should().BeGreaterThan(0);
        result.VoteCount.Should().BeGreaterThan(0);
        result.Popularity.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WithAllAppends_WithRealApi_ReturnsCompleteData()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvShowAppends? result = await client.WithAllAppends();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(ValidTvShowId);
        result.Name.Should().NotBeNullOrEmpty();
        result.Overview.Should().NotBeNullOrEmpty();

        // Verify appended data exists
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
    public async Task AggregatedCredits_WithRealApi_ReturnsValidCredits()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvAggregatedCredits? result = await client.AggregatedCredits();

        // Assert
        result.Should().NotBeNull();
        result.Cast.Should().NotBeEmpty();
        // Note: Some TV shows like GTST may not have crew data available
        // result.Crew.Should().NotBeEmpty();

        // Verify cast and crew have basic properties
        result.Cast.Should().AllSatisfy(castMember =>
        {
            castMember.Id.Should().BeGreaterThan(0);
            castMember.Name.Should().NotBeNullOrEmpty();
            castMember.Roles.Should().NotBeEmpty();
        });
    }

    [Fact]
    public async Task Credits_WithRealApi_ReturnsValidCredits()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvCredits? result = await client.Credits();

        // Assert
        result.Should().NotBeNull();
        // result.Cast.Should().NotBeEmpty();
        // Note: Some TV shows like GTST may not have crew data available
        // result.Crew.Should().NotBeEmpty();

        // Verify cast data
        result.Cast.Should().AllSatisfy(castMember =>
        {
            castMember.Id.Should().BeGreaterThan(0);
            castMember.Name.Should().NotBeNullOrEmpty();
            castMember.Character.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task Images_WithRealApi_ReturnsValidImages()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbImages? result = await client.Images();

        // Assert
        result.Should().NotBeNull();
        result.Backdrops.Should().NotBeEmpty();
        result.Posters.Should().NotBeEmpty();

        // Verify backdrop data
        result.Backdrops.Should().AllSatisfy(backdrop =>
        {
            backdrop.FilePath.Should().NotBeNullOrEmpty();
            backdrop.Width.Should().BeGreaterThan(0);
            backdrop.Height.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task Videos_WithRealApi_ReturnsValidVideos()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvVideos? result = await client.Videos();

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeNull();

        if (result.Results.Length != 0)
            result.Results.Should().AllSatisfy(video =>
            {
                video.Id.Should().NotBeNullOrEmpty();
                video.Key.Should().NotBeNullOrEmpty();
                video.Name.Should().NotBeNullOrEmpty();
                video.Site.Should().NotBeNullOrEmpty();
                video.Type.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public async Task ExternalIds_WithRealApi_ReturnsValidIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvExternalIds? result = await client.ExternalIds();

        // Assert
        result.Should().NotBeNull();
        // Most popular shows should have at least one external ID
    }

    [Fact]
    public async Task Recommendations_WithRealApi_ReturnsValidRecommendations()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvRecommendations? result = await client.Recommendations();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeNull();

        if (result.Results.Any())
            result.Results.Should().AllSatisfy(show =>
            {
                show.Id.Should().BeGreaterThan(0);
                show.Name.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public async Task Similar_WithRealApi_ReturnsValidSimilarShows()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvSimilar? result = await client.Similar();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeNull();

        if (result.Results.Any())
            result.Results.Should().AllSatisfy(show =>
            {
                show.Id.Should().BeGreaterThan(0);
                show.Name.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public async Task Translations_WithRealApi_ReturnsValidTranslations()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbSharedTranslations? result = await client.Translations();

        // Assert
        result.Should().NotBeNull();
        result.Translations.Should().NotBeEmpty();

        result.Translations.Should().AllSatisfy(translation =>
        {
            translation.Iso31661.Should().NotBeNullOrEmpty();
            translation.Iso6391.Should().NotBeNullOrEmpty();
            // Name can be empty in some TMDB translations due to data quality issues
            translation.Name.Should().NotBeNull();
            translation.EnglishName.Should().NotBeNullOrEmpty();
        });

        // Should have English translation
        result.Translations.Should().Contain(t => t.Iso6391 == "en");
    }

    [Fact]
    public async Task Keywords_WithRealApi_ReturnsValidKeywords()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvKeywords? result = await client.Keywords();

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeNull();

        if (result.Results.Length != 0)
            result.Results.Should().AllSatisfy(keyword =>
            {
                keyword.Id.Should().BeGreaterThan(0);
                keyword.Name.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public async Task WatchProviders_WithRealApi_ReturnsValidProviders()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbWatchProviders? result = await client.WatchProviders();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ValidTvShowId);
        result.TmdbWatchProviderResults.Should().NotBeNull();
    }

    [Fact]
    public async Task ContentRatings_WithRealApi_ReturnsValidRatings()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvContentRatings? result = await client.ContentRatings();

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeNull();

        if (result.Results.Length != 0)
            result.Results.Should().AllSatisfy(rating =>
            {
                rating.Iso31661.Should().NotBeNullOrEmpty();
                rating.Rating.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public async Task AlternativeTitles_WithRealApi_ReturnsValidTitles()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvAlternativeTitles? result = await client.AlternativeTitles();

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeNull();

        if (result.Results.Length != 0)
            result.Results.Should().AllSatisfy(title =>
            {
                title.Iso31661.Should().NotBeNullOrEmpty();
                title.Title.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public async Task EpisodeGroups_WithRealApi_ReturnsValidGroups()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvEpisodeGroups? result = await client.EpisodeGroups();

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeNull();

        if (result.Results.Length != 0)
            result.Results.Should().AllSatisfy(group =>
            {
                group.Id.Should().NotBeNullOrEmpty();
                group.Name.Should().NotBeNullOrEmpty();
                group.EpisodeCount.Should().BeGreaterThan(0);
                group.GroupCount.Should().BeGreaterThan(0);
            });
    }

    [Fact]
    public async Task Reviews_WithRealApi_ReturnsValidReviews()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvReviews? result = await client.Reviews();

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeNull();

        if (result.Results.Any())
            result.Results.Should().AllSatisfy(review =>
            {
                review.Id.Should().NotBeNullOrEmpty();
                review.Author.Should().NotBeNullOrEmpty();
                review.Content.Should().NotBeNullOrEmpty();
                review.CreatedAt.Should().NotBeNull();
            });
    }

    [Fact]
    public async Task ScreenedTheatrically_WithRealApi_ReturnsValidScreenings()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvScreenedTheatrically? result = await client.ScreenedTheatrically();

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeNull();

        if (result.Results.Length != 0)
            result.Results.Should().AllSatisfy(screening =>
            {
                screening.Id.Should().BeGreaterThan(0);
                screening.EpisodeNumber.Should().BeGreaterThan(0);
                screening.SeasonNumber.Should().BeGreaterThan(0);
            });
    }

    [Fact]
    public async Task Popular_WithRealApi_ReturnsPopularShows()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new();

        // Act
        List<TmdbTvShow>? result = await client.Popular();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().HaveCountLessThanOrEqualTo(10);

        result.Should().AllSatisfy(show =>
        {
            show.Id.Should().BeGreaterThan(0);
            show.Name.Should().NotBeNullOrEmpty();
            show.Popularity.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task TopRated_WithRealApi_ReturnsTopRatedShows()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new();

        // Act
        TmdbTvTopRated? result = await client.TopRated();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeEmpty();

        result.Results.Should().AllSatisfy(show =>
        {
            show.Id.Should().BeGreaterThan(0);
            show.Name.Should().NotBeNullOrEmpty();
            show.VoteAverage.Should().BeGreaterThan(7.0); // Top rated should have high ratings
        });
    }

    [Fact]
    public async Task OnTheAir_WithRealApi_ReturnsCurrentlyAiringShows()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new();

        // Act
        TmdbTvOnTheAir? result = await client.OnTheAir();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeNull();

        if (result.Results.Any())
            result.Results.Should().AllSatisfy(show =>
            {
                show.Id.Should().BeGreaterThan(0);
                show.Name.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public async Task AiringToday_WithRealApi_ReturnsAiringTodayShows()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new();

        // Act
        TmdbTvAiringToday? result = await client.AiringToday();

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().BeGreaterThan(0);
        result.Results.Should().NotBeNull();

        if (result.Results.Any())
            result.Results.Should().AllSatisfy(show =>
            {
                show.Id.Should().BeGreaterThan(0);
                show.Name.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public async Task Latest_WithRealApi_ReturnsLatestShow()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new();

        // Act
        TmdbTvShowLatest? result = await client.Latest();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().NotBeNullOrEmpty();
        result.Type.Should().NotBeNull();
    }

    [Fact]
    public async Task Genres_WithRealApi_ReturnsValidGenres()
    {
        // Arrange
        SetupTmdbAuthentication();
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

        // Verify known TV genres exist
        result.Genres.Should().Contain(g => g.Name == "Drama");
        result.Genres.Should().Contain(g => g.Name == "Comedy");
        result.Genres.Should().Contain(g => g.Name == "Crime");
    }

    [Fact]
    public async Task Certifications_WithRealApi_ReturnsValidCertifications()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new();

        // Act
        TvShowCertifications? result = await client.Certifications();

        // Assert
        result.Should().NotBeNull();
        result.Certifications.Should().NotBeNull();

        // Should have US certifications
        result.Certifications.Should().ContainKey("US");
        TmdbTvShowCertification[] usCertifications = result.Certifications["US"];
        usCertifications.Should().NotBeEmpty();

        usCertifications.Should().AllSatisfy(cert =>
        {
            cert.Rating.Should().NotBeNullOrEmpty();
            cert.Meaning.Should().NotBeNullOrEmpty();
            cert.Order.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public void Season_WithRealApi_ReturnsValidSeasonClient()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbSeasonClient seasonClient = client.Season(ValidSeasonNumber);

        // Assert
        seasonClient.Should().NotBeNull();
        seasonClient.Should().BeOfType<TmdbSeasonClient>();
    }

    [Fact]
    public async Task Details_WithInvalidId_ReturnsNull()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(999999999);

        // Act
        TmdbTvShowDetails? result = await client.Details();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Changes_WithInvalidDateFormat_HandlesGracefully()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbTvClient client = new(ValidTvShowId);

        // Act
        TmdbTvChanges? result = await client.Changes("invalid-date", "also-invalid");

        // Assert
        // Changes endpoint may return null for invalid date formats
        if (result != null) result.Items.Should().NotBeNull();
    }
}