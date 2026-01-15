using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Tests.Providers.TMDB.Mocks;

namespace NoMercy.Tests.Providers.TMDB.Models;

/// <summary>
/// Tests for TMDB Movie model classes to verify serialization/deserialization
/// and data integrity
/// </summary>
public class TmdbMovieModelsTests
{
    [Fact]
    public void TmdbMovieDetails_SerializeDeserialize_MaintainsDataIntegrity()
    {
        // Arrange
        TmdbMovieDetails originalMovie = TmdbMovieMockData.GetSampleMovieDetails();

        // Act
        string json = JsonConvert.SerializeObject(originalMovie);
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(json);

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.Id.Should().Be(originalMovie.Id);
        deserializedMovie.Title.Should().Be(originalMovie.Title);
        deserializedMovie.OriginalTitle.Should().Be(originalMovie.OriginalTitle);
        deserializedMovie.Overview.Should().Be(originalMovie.Overview);
        deserializedMovie.Adult.Should().Be(originalMovie.Adult);
        deserializedMovie.Budget.Should().Be(originalMovie.Budget);
        deserializedMovie.Revenue.Should().Be(originalMovie.Revenue);
        deserializedMovie.Runtime.Should().Be(originalMovie.Runtime);
        deserializedMovie.Status.Should().Be(originalMovie.Status);
        deserializedMovie.Tagline.Should().Be(originalMovie.Tagline);
        deserializedMovie.ReleaseDate.Should().Be(originalMovie.ReleaseDate);
        deserializedMovie.OriginalLanguage.Should().Be(originalMovie.OriginalLanguage);
        deserializedMovie.Popularity.Should().Be(originalMovie.Popularity);
        deserializedMovie.VoteAverage.Should().Be(originalMovie.VoteAverage);
        deserializedMovie.VoteCount.Should().Be(originalMovie.VoteCount);
        deserializedMovie.Video.Should().Be(originalMovie.Video);
        deserializedMovie.ImdbId.Should().Be(originalMovie.ImdbId);
        deserializedMovie.Homepage.Should().Be(originalMovie.Homepage);
    }

    [Fact]
    public void TmdbMovieDetails_WithMinimalData_DeserializesCorrectly()
    {
        // Arrange
        TmdbMovieDetails minimalMovie = TmdbMovieMockData.GetMinimalMovieDetails();

        // Act
        string json = JsonConvert.SerializeObject(minimalMovie);
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(json);

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.Id.Should().Be(minimalMovie.Id);
        deserializedMovie.Title.Should().Be(minimalMovie.Title);
        deserializedMovie.OriginalTitle.Should().Be(minimalMovie.OriginalTitle);
        deserializedMovie.Adult.Should().Be(minimalMovie.Adult);
        deserializedMovie.Status.Should().Be(minimalMovie.Status);
        deserializedMovie.ReleaseDate.Should().Be(minimalMovie.ReleaseDate);
        deserializedMovie.OriginalLanguage.Should().Be(minimalMovie.OriginalLanguage);
    }

    [Fact]
    public void TmdbMovieCredits_SerializeDeserialize_MaintainsDataIntegrity()
    {
        // Arrange
        TmdbMovieCredits originalCredits = TmdbMovieMockData.GetSampleMovieCredits();

        // Act
        string json = JsonConvert.SerializeObject(originalCredits);
        TmdbMovieCredits? deserializedCredits = JsonConvert.DeserializeObject<TmdbMovieCredits>(json);

        // Assert
        deserializedCredits.Should().NotBeNull();
        deserializedCredits!.Id.Should().Be(originalCredits.Id);
        deserializedCredits.Cast.Should().HaveCount(originalCredits.Cast.Length);
        deserializedCredits.Crew.Should().HaveCount(originalCredits.Crew.Length);

        // Verify cast data
        for (int i = 0; i < originalCredits.Cast.Length; i++)
        {
            TmdbCast originalCast = originalCredits.Cast[i];
            TmdbCast deserializedCast = deserializedCredits.Cast[i];

            deserializedCast.Id.Should().Be(originalCast.Id);
            deserializedCast.Name.Should().Be(originalCast.Name);
            deserializedCast.Character.Should().Be(originalCast.Character);
            deserializedCast.Order.Should().Be(originalCast.Order);
            deserializedCast.CreditId.Should().Be(originalCast.CreditId);
            deserializedCast.Gender.Should().Be(originalCast.Gender);
            deserializedCast.KnownForDepartment.Should().Be(originalCast.KnownForDepartment);
            deserializedCast.OriginalName.Should().Be(originalCast.OriginalName);
            deserializedCast.Popularity.Should().Be(originalCast.Popularity);
            deserializedCast.ProfilePath.Should().Be(originalCast.ProfilePath);
        }

        // Verify crew data
        for (int i = 0; i < originalCredits.Crew.Length; i++)
        {
            TmdbCrew originalCrew = originalCredits.Crew[i];
            TmdbCrew deserializedCrew = deserializedCredits.Crew[i];

            deserializedCrew.Id.Should().Be(originalCrew.Id);
            deserializedCrew.Name.Should().Be(originalCrew.Name);
            deserializedCrew.Job.Should().Be(originalCrew.Job);
            deserializedCrew.Department.Should().Be(originalCrew.Department);
            deserializedCrew.CreditId.Should().Be(originalCrew.CreditId);
            deserializedCrew.Gender.Should().Be(originalCrew.Gender);
            deserializedCrew.KnownForDepartment.Should().Be(originalCrew.KnownForDepartment);
            deserializedCrew.OriginalName.Should().Be(originalCrew.OriginalName);
            deserializedCrew.Popularity.Should().Be(originalCrew.Popularity);
            deserializedCrew.ProfilePath.Should().Be(originalCrew.ProfilePath);
        }
    }

    [Fact]
    public void TmdbMovieExternalIds_SerializeDeserialize_MaintainsDataIntegrity()
    {
        // Arrange
        TmdbMovieExternalIds originalExternalIds = TmdbMovieMockData.GetSampleMovieExternalIds();

        // Act
        string json = JsonConvert.SerializeObject(originalExternalIds);
        TmdbMovieExternalIds? deserializedExternalIds = JsonConvert.DeserializeObject<TmdbMovieExternalIds>(json);

        // Assert
        deserializedExternalIds.Should().NotBeNull();
        deserializedExternalIds!.Id.Should().Be(originalExternalIds.Id);
        deserializedExternalIds.ImdbId.Should().Be(originalExternalIds.ImdbId);
        deserializedExternalIds.FacebookId.Should().Be(originalExternalIds.FacebookId);
        deserializedExternalIds.InstagramId.Should().Be(originalExternalIds.InstagramId);
        deserializedExternalIds.TwitterId.Should().Be(originalExternalIds.TwitterId);
    }

    [Fact]
    public void TmdbMovieAppends_SerializeDeserialize_MaintainsDataIntegrity()
    {
        // Arrange
        TmdbMovieAppends originalAppends = TmdbMovieMockData.GetSampleMovieAppends();

        // Act
        string json = JsonConvert.SerializeObject(originalAppends);
        TmdbMovieAppends? deserializedAppends = JsonConvert.DeserializeObject<TmdbMovieAppends>(json);

        // Assert
        deserializedAppends.Should().NotBeNull();
        deserializedAppends!.Id.Should().Be(originalAppends.Id);
        deserializedAppends.Title.Should().Be(originalAppends.Title);
        deserializedAppends.OriginalTitle.Should().Be(originalAppends.OriginalTitle);
        
        // Verify nested objects
        deserializedAppends.Credits.Should().NotBeNull();
        deserializedAppends.Credits!.Id.Should().Be(originalAppends.Credits!.Id);
        
        deserializedAppends.ExternalIds.Should().NotBeNull();
        deserializedAppends.ExternalIds!.Id.Should().Be(originalAppends.ExternalIds!.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void TmdbMovieDetails_WithVariousIds_HandlesCorrectly(int movieId)
    {
        // Arrange
        TmdbMovieDetails movie = TmdbMovieMockData.GenerateMovieWithId(movieId);

        // Act
        string json = JsonConvert.SerializeObject(movie);
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(json);

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.Id.Should().Be(movieId);
    }

    [Fact]
    public void TmdbMovieDetails_WithNullOptionalFields_DeserializesCorrectly()
    {
        // Arrange
        string movieJson = """
                           {
                               "id": 12345,
                               "title": "Test Movie",
                               "original_title": "Test Movie",
                               "adult": false,
                               "status": "Released",
                               "release_date": "2024-01-01T00:00:00",
                               "original_language": "en",
                               "overview": null,
                               "tagline": null,
                               "homepage": null,
                               "imdb_id": null,
                               "backdrop_path": null,
                               "poster_path": null,
                               "video": null
                           }
                           """;

        // Act
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(movieJson);

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.Id.Should().Be(12345);
        deserializedMovie.Title.Should().Be("Test Movie");
        deserializedMovie.Overview.Should().BeNull();
        deserializedMovie.Tagline.Should().BeNull();
        deserializedMovie.Homepage.Should().BeNull();
        deserializedMovie.ImdbId.Should().BeNull();
        deserializedMovie.BackdropPath.Should().BeNull();
        deserializedMovie.PosterPath.Should().BeNull();
        deserializedMovie.Video.Should().BeNull();
    }

    [Fact]
    public void TmdbMovieCredits_WithEmptyArrays_DeserializesCorrectly()
    {
        // Arrange
        string creditsJson = """
                             {
                                 "id": 12345,
                                 "cast": [],
                                 "crew": []
                             }
                             """;

        // Act
        TmdbMovieCredits? deserializedCredits = JsonConvert.DeserializeObject<TmdbMovieCredits>(creditsJson);

        // Assert
        deserializedCredits.Should().NotBeNull();
        deserializedCredits!.Id.Should().Be(12345);
        deserializedCredits.Cast.Should().BeEmpty();
        deserializedCredits.Crew.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1990-05-15")]
    [InlineData("2024-12-31")]
    [InlineData("2000-02-29")] // Leap year
    public void TmdbMovieDetails_WithVariousReleaseDates_ParsesCorrectly(string dateString)
    {
        // Arrange
        string movieJson = $$"""
                             {
                                 "id": 12345,
                                 "title": "Test Movie",
                                 "original_title": "Test Movie",
                                 "adult": false,
                                 "status": "Released",
                                 "release_date": "{{dateString}}T00:00:00",
                                 "original_language": "en"
                             }
                             """;

        // Act
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(movieJson);

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.ReleaseDate.Should().NotBeNull();
        deserializedMovie.ReleaseDate!.Value.ToString("yyyy-MM-dd").Should().Be(dateString);
    }
}
