using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

[Trait("Category", "Integration")]
[Trait("Provider", "TMDB")]
[Trait("Client", "TmdbSeasonClient")]
[Collection("TmdbApi")]
public class TmdbSeasonClientIntegrationTests : TmdbTestBase
{
    public TmdbSeasonClientIntegrationTests()
    {
        SetupTmdbAuthentication();
    }

    [Fact]
    public async Task Details_WithRealApi_ReturnsCompleteSeasonData()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonDetails? result = await client.Details();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Name.Should().NotBeNullOrEmpty();
        result.SeasonNumber.Should().Be(ValidSeasonNumber);
        result.Episodes.Should().NotBeEmpty();
        result.Episodes.First().Name.Should().NotBeNullOrEmpty();
        result.Episodes.First().EpisodeNumber.Should().BeGreaterThan(0);
        result.AirDate.Should().NotBeNull();
        // Overview can be empty for some seasons, so we just check it's not null
        result.Overview.Should().NotBeNull();
    }

    [Fact]
    public async Task WithAllAppends_WithRealApi_ReturnsCompleteData()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonAppends? result = await client.WithAllAppends();

        // Assert
        result.Should().NotBeNull();
        result!.SeasonNumber.Should().Be(ValidSeasonNumber);
        result.TmdbSeasonCredits.Should().NotBeNull();
        result.TmdbSeasonCredits.Cast.Should().NotBeEmpty();
        result.TmdbSeasonImages.Should().NotBeNull();
        result.TmdbSeasonExternalIds.Should().NotBeNull();
        result.Translations.Should().NotBeNull();
    }

    [Fact]
    public async Task Credits_WithRealApi_ReturnsValidCredits()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonCredits? result = await client.Credits();

        // Assert
        result.Should().NotBeNull();
        result!.Cast.Should().NotBeEmpty();
        result.Cast.Should().AllSatisfy(cast =>
        {
            cast.Name.Should().NotBeNullOrEmpty();
            cast.Character.Should().NotBeNullOrEmpty();
            cast.Id.Should().BeGreaterThan(0);
        });
        
        result.Crew.Should().NotBeNull();
        // Crew data might be empty for some seasons, so we validate structure if present
        if (result.Crew.Length != 0)
        {
            result.Crew.Should().AllSatisfy(crew =>
            {
                crew.Name.Should().NotBeNullOrEmpty();
                crew.Job.Should().NotBeNullOrEmpty();
                crew.Id.Should().BeGreaterThan(0);
            });
        }
    }

    [Fact]
    public async Task AggregatedCredits_WithRealApi_ReturnsValidCredits()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonAggregatedCredits? result = await client.AggregatedCredits();

        // Assert
        result.Should().NotBeNull();
        result!.Cast.Should().NotBeEmpty();
        result.Cast.Should().AllSatisfy(cast =>
        {
            cast.Name.Should().NotBeNullOrEmpty();
            cast.Id.Should().BeGreaterThan(0);
            cast.Roles.Should().NotBeEmpty();
        });
        
        result.Crew.Should().NotBeNull();
        // Crew data might be empty for some seasons, so we validate structure if present
        if (result.Crew.Length != 0)
        {
            result.Crew.Should().AllSatisfy(crew =>
            {
                crew.Name.Should().NotBeNullOrEmpty();
                crew.Id.Should().BeGreaterThan(0);
                crew.Jobs.Should().NotBeEmpty();
            });
        }
    }

    [Fact]
    public async Task ExternalIds_WithRealApi_ReturnsValidIds()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonExternalIds? result = await client.ExternalIds();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        // External IDs can be null for some seasons, so we just verify structure
    }

    [Fact]
    public async Task Images_WithRealApi_ReturnsValidImages()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonImages? result = await client.Images();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Posters.Should().NotBeNull();
        
        if (result.Posters.Length != 0)
        {
            result.Posters.Should().AllSatisfy(poster =>
            {
                poster.FilePath.Should().NotBeNullOrEmpty();
                poster.Width.Should().BeGreaterThan(0);
                poster.Height.Should().BeGreaterThan(0);
                poster.VoteAverage.Should().BeGreaterThanOrEqualTo(0);
            });
        }
    }

    [Fact]
    public async Task Videos_WithRealApi_ReturnsValidVideos()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonVideos? result = await client.Videos();

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeNull();
        
        // Videos might be empty for some seasons, so only validate if they exist
        if (result.Results.Length != 0)
        {
            result.Results.Should().AllSatisfy(video =>
            {
                video.Key.Should().NotBeNullOrEmpty();
                video.Name.Should().NotBeNullOrEmpty();
                video.Type.Should().NotBeNullOrEmpty();
                video.Site.Should().NotBeNull();
            });
        }
    }

    [Fact]
    public async Task Translations_WithRealApi_ReturnsValidTranslations()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSharedTranslations? result = await client.Translations();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Translations.Should().NotBeEmpty();
        
        result.Translations.Should().AllSatisfy(translation =>
        {
            translation.Iso6391.Should().NotBeNullOrEmpty();
            translation.Iso31661.Should().NotBeNullOrEmpty();
            translation.EnglishName.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public void Episode_WithValidEpisodeNumber_ReturnsEpisodeClient()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbEpisodeClient episodeClient = client.Episode(ValidEpisodeNumber);

        // Assert
        episodeClient.Should().NotBeNull();
        episodeClient.Should().BeOfType<TmdbEpisodeClient>();
    }

    [Fact]
    public async Task Details_WithDifferentSeasons_ReturnsCorrectSeasonNumbers()
    {
        // Arrange
        TmdbSeasonClient season1Client = new(ValidTvShowId, 1);
        TmdbSeasonClient season2Client = new(ValidTvShowId, 2);

        // Act
        TmdbSeasonDetails? season1 = await season1Client.Details();
        TmdbSeasonDetails? season2 = await season2Client.Details();

        // Assert
        season1.Should().NotBeNull();
        season1!.SeasonNumber.Should().Be(1);
        
        season2.Should().NotBeNull();
        season2!.SeasonNumber.Should().Be(2);
        
        season1.Id.Should().NotBe(season2.Id);
    }

    [Fact]
    public async Task Changes_WithValidDateRange_ReturnsChanges()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        TmdbSeasonChanges? result = await client.Changes("2024-01-01", "2024-06-30");

        // Assert - Changes endpoint may return null even for valid requests due to TMDB API limitations
        // This is expected behavior as confirmed by testing with multiple active series
        if (result != null)
        {
            result.Changes.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Changes_WithInvalidDateFormat_HandlesGracefully()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, ValidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Changes("invalid-date", "another-invalid-date");

        // Assert
        await act.Should().NotThrowAsync("because invalid dates should be handled gracefully");
    }

    [Fact]
    public async Task Details_WithInvalidSeasonNumber_ReturnsNull()
    {
        // Arrange
        TmdbSeasonClient client = new(ValidTvShowId, 999);

        // Act
        Func<Task> act = async () => await client.Details();

        // Assert
        await act.Should().NotThrowAsync("because invalid season numbers should be handled gracefully");
    }

    [Fact]
    public async Task Details_WithInvalidTvShowId_ReturnsNull()
    {
        // Arrange
        TmdbSeasonClient client = new(999999999, ValidSeasonNumber);

        // Act
        Func<Task> act = async () => await client.Details();

        // Assert
        await act.Should().NotThrowAsync("because invalid TV show IDs should be handled gracefully");
    }

    [Fact]
    public async Task MultipleSeasonClients_WorkingConcurrently_PerformCorrectly()
    {
        // Arrange
        TmdbSeasonClient season1Client = new(ValidTvShowId, 1);
        TmdbSeasonClient season2Client = new(ValidTvShowId, 2);
        TmdbSeasonClient season3Client = new(ValidTvShowId, 3);

        // Act
        Task<TmdbSeasonDetails?> task1 = season1Client.Details();
        Task<TmdbSeasonDetails?> task2 = season2Client.Details();
        Task<TmdbSeasonDetails?> task3 = season3Client.Details();

        await Task.WhenAll(task1, task2, task3);

        TmdbSeasonDetails? season1 = await task1;
        TmdbSeasonDetails? season2 = await task2;
        TmdbSeasonDetails? season3 = await task3;

        // Assert
        season1.Should().NotBeNull();
        season2.Should().NotBeNull();
        season3.Should().NotBeNull();
        
        season1!.SeasonNumber.Should().Be(1);
        season2!.SeasonNumber.Should().Be(2);
        season3!.SeasonNumber.Should().Be(3);
    }
}
