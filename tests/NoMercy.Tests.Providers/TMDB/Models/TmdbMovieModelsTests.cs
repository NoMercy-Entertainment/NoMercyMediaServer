// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
        string json = JsonConvert.SerializeObject(value: originalMovie);
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(value: json);

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.Id.Should().Be(expected: originalMovie.Id);
        deserializedMovie.Title.Should().Be(expected: originalMovie.Title);
        deserializedMovie.OriginalTitle.Should().Be(expected: originalMovie.OriginalTitle);
        deserializedMovie.Overview.Should().Be(expected: originalMovie.Overview);
        deserializedMovie.Adult.Should().Be(expected: originalMovie.Adult);
        deserializedMovie.Budget.Should().Be(expected: originalMovie.Budget);
        deserializedMovie.Revenue.Should().Be(expected: originalMovie.Revenue);
        deserializedMovie.Runtime.Should().Be(expected: originalMovie.Runtime);
        deserializedMovie.Status.Should().Be(expected: originalMovie.Status);
        deserializedMovie.Tagline.Should().Be(expected: originalMovie.Tagline);
        deserializedMovie.ReleaseDate.Should().Be(expected: originalMovie.ReleaseDate);
        deserializedMovie.OriginalLanguage.Should().Be(expected: originalMovie.OriginalLanguage);
        deserializedMovie.Popularity.Should().Be(expected: originalMovie.Popularity);
        deserializedMovie.VoteAverage.Should().Be(expected: originalMovie.VoteAverage);
        deserializedMovie.VoteCount.Should().Be(expected: originalMovie.VoteCount);
        deserializedMovie.Video.Should().Be(expected: originalMovie.Video);
        deserializedMovie.ImdbId.Should().Be(expected: originalMovie.ImdbId);
        deserializedMovie.Homepage.Should().Be(expected: originalMovie.Homepage);
    }

    [Fact]
    public void TmdbMovieDetails_WithMinimalData_DeserializesCorrectly()
    {
        // Arrange
        TmdbMovieDetails minimalMovie = TmdbMovieMockData.GetMinimalMovieDetails();

        // Act
        string json = JsonConvert.SerializeObject(value: minimalMovie);
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(value: json);

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.Id.Should().Be(expected: minimalMovie.Id);
        deserializedMovie.Title.Should().Be(expected: minimalMovie.Title);
        deserializedMovie.OriginalTitle.Should().Be(expected: minimalMovie.OriginalTitle);
        deserializedMovie.Adult.Should().Be(expected: minimalMovie.Adult);
        deserializedMovie.Status.Should().Be(expected: minimalMovie.Status);
        deserializedMovie.ReleaseDate.Should().Be(expected: minimalMovie.ReleaseDate);
        deserializedMovie.OriginalLanguage.Should().Be(expected: minimalMovie.OriginalLanguage);
    }

    [Fact]
    public void TmdbMovieCredits_SerializeDeserialize_MaintainsDataIntegrity()
    {
        // Arrange
        TmdbMovieCredits originalCredits = TmdbMovieMockData.GetSampleMovieCredits();

        // Act
        string json = JsonConvert.SerializeObject(value: originalCredits);
        TmdbMovieCredits? deserializedCredits = JsonConvert.DeserializeObject<TmdbMovieCredits>(
            value: json
        );

        // Assert
        deserializedCredits.Should().NotBeNull();
        deserializedCredits!.Id.Should().Be(expected: originalCredits.Id);
        deserializedCredits.Cast.Should().HaveCount(expected: originalCredits.Cast.Length);
        deserializedCredits.Crew.Should().HaveCount(expected: originalCredits.Crew.Length);

        // Verify cast data
        for (int i = 0; i < originalCredits.Cast.Length; i++)
        {
            TmdbCast originalCast = originalCredits.Cast[i];
            TmdbCast deserializedCast = deserializedCredits.Cast[i];

            deserializedCast.Id.Should().Be(expected: originalCast.Id);
            deserializedCast.Name.Should().Be(expected: originalCast.Name);
            deserializedCast.Character.Should().Be(expected: originalCast.Character);
            deserializedCast.Order.Should().Be(expected: originalCast.Order);
            deserializedCast.CreditId.Should().Be(expected: originalCast.CreditId);
            deserializedCast.Gender.Should().Be(expected: originalCast.Gender);
            deserializedCast.KnownForDepartment.Should().Be(expected: originalCast.KnownForDepartment);
            deserializedCast.OriginalName.Should().Be(expected: originalCast.OriginalName);
            deserializedCast.Popularity.Should().Be(expected: originalCast.Popularity);
            deserializedCast.ProfilePath.Should().Be(expected: originalCast.ProfilePath);
        }

        // Verify crew data
        for (int i = 0; i < originalCredits.Crew.Length; i++)
        {
            TmdbCrew originalCrew = originalCredits.Crew[i];
            TmdbCrew deserializedCrew = deserializedCredits.Crew[i];

            deserializedCrew.Id.Should().Be(expected: originalCrew.Id);
            deserializedCrew.Name.Should().Be(expected: originalCrew.Name);
            deserializedCrew.Job.Should().Be(expected: originalCrew.Job);
            deserializedCrew.Department.Should().Be(expected: originalCrew.Department);
            deserializedCrew.CreditId.Should().Be(expected: originalCrew.CreditId);
            deserializedCrew.Gender.Should().Be(expected: originalCrew.Gender);
            deserializedCrew.KnownForDepartment.Should().Be(expected: originalCrew.KnownForDepartment);
            deserializedCrew.OriginalName.Should().Be(expected: originalCrew.OriginalName);
            deserializedCrew.Popularity.Should().Be(expected: originalCrew.Popularity);
            deserializedCrew.ProfilePath.Should().Be(expected: originalCrew.ProfilePath);
        }
    }

    [Fact]
    public void TmdbMovieExternalIds_SerializeDeserialize_MaintainsDataIntegrity()
    {
        // Arrange
        TmdbMovieExternalIds originalExternalIds = TmdbMovieMockData.GetSampleMovieExternalIds();

        // Act
        string json = JsonConvert.SerializeObject(value: originalExternalIds);
        TmdbMovieExternalIds? deserializedExternalIds =
            JsonConvert.DeserializeObject<TmdbMovieExternalIds>(value: json);

        // Assert
        deserializedExternalIds.Should().NotBeNull();
        deserializedExternalIds!.Id.Should().Be(expected: originalExternalIds.Id);
        deserializedExternalIds.ImdbId.Should().Be(expected: originalExternalIds.ImdbId);
        deserializedExternalIds.FacebookId.Should().Be(expected: originalExternalIds.FacebookId);
        deserializedExternalIds.InstagramId.Should().Be(expected: originalExternalIds.InstagramId);
        deserializedExternalIds.TwitterId.Should().Be(expected: originalExternalIds.TwitterId);
    }

    [Fact]
    public void TmdbMovieAppends_SerializeDeserialize_MaintainsDataIntegrity()
    {
        // Arrange
        TmdbMovieAppends originalAppends = TmdbMovieMockData.GetSampleMovieAppends();

        // Act
        string json = JsonConvert.SerializeObject(value: originalAppends);
        TmdbMovieAppends? deserializedAppends = JsonConvert.DeserializeObject<TmdbMovieAppends>(
            value: json
        );

        // Assert
        deserializedAppends.Should().NotBeNull();
        deserializedAppends!.Id.Should().Be(expected: originalAppends.Id);
        deserializedAppends.Title.Should().Be(expected: originalAppends.Title);
        deserializedAppends.OriginalTitle.Should().Be(expected: originalAppends.OriginalTitle);

        // Verify nested objects
        deserializedAppends.Credits.Should().NotBeNull();
        deserializedAppends.Credits!.Id.Should().Be(expected: originalAppends.Credits!.Id);

        deserializedAppends.ExternalIds.Should().NotBeNull();
        deserializedAppends.ExternalIds!.Id.Should().Be(expected: originalAppends.ExternalIds!.Id);
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: -1)]
    [InlineData(data: int.MaxValue)]
    [InlineData(data: int.MinValue)]
    public void TmdbMovieDetails_WithVariousIds_HandlesCorrectly(int movieId)
    {
        // Arrange
        TmdbMovieDetails movie = TmdbMovieMockData.GenerateMovieWithId(id: movieId);

        // Act
        string json = JsonConvert.SerializeObject(value: movie);
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(value: json);

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.Id.Should().Be(expected: movieId);
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
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(
            value: movieJson
        );

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.Id.Should().Be(expected: 12345);
        deserializedMovie.Title.Should().Be(expected: "Test Movie");
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
        TmdbMovieCredits? deserializedCredits = JsonConvert.DeserializeObject<TmdbMovieCredits>(
            value: creditsJson
        );

        // Assert
        deserializedCredits.Should().NotBeNull();
        deserializedCredits!.Id.Should().Be(expected: 12345);
        deserializedCredits.Cast.Should().BeEmpty();
        deserializedCredits.Crew.Should().BeEmpty();
    }

    [Theory]
    [InlineData(data: "1990-05-15")]
    [InlineData(data: "2024-12-31")]
    [InlineData(data: "2000-02-29")] // Leap year
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
        TmdbMovieDetails? deserializedMovie = JsonConvert.DeserializeObject<TmdbMovieDetails>(
            value: movieJson
        );

        // Assert
        deserializedMovie.Should().NotBeNull();
        deserializedMovie!.ReleaseDate.Should().NotBeNull();
        deserializedMovie.ReleaseDate!.Value.ToString(format: "yyyy-MM-dd").Should().Be(expected: dateString);
    }
}
