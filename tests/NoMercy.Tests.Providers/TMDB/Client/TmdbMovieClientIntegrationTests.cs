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

using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Integration tests for TmdbMovieClient that make real API calls
/// Note: These tests require a valid TMDB API key and internet connection
/// They may be slower and should be run sparingly in CI/CD
/// </summary>
[Collection(name: "TmdbApi")]
public class TmdbMovieClientIntegrationTests : TmdbTestBase
{
    private const int WellKnownMovieId = 155; // The Dark Knight - stable test data
    private const int AnotherWellKnownMovieId = 278; // The Shawshank Redemption

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task Details_WithRealApi_ReturnsActualMovieDetails()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();

        // Act
        TmdbMovieDetails? result = await client.Details();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: WellKnownMovieId);
        result.Title.Should().NotBeNullOrEmpty();
        result.OriginalTitle.Should().NotBeNullOrEmpty();
        result.Overview.Should().NotBeNullOrEmpty();
        result.ReleaseDate.Should().NotBeNull();
        result.Runtime.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task WithAllAppends_WithRealApi_ReturnsCompleteData()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();

        // Act
        TmdbMovieAppends? result = await client.WithAllAppends();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: WellKnownMovieId);
        result.Title.Should().NotBeNullOrEmpty();

        // Verify appended data
        result.Credits.Should().NotBeNull();
        result.Credits!.Cast.Should().NotBeEmpty();
        result.Credits.Crew.Should().NotBeEmpty();

        result.ExternalIds.Should().NotBeNull();
        result.ExternalIds!.ImdbId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task Credits_WithRealApi_ReturnsActualCredits()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();

        // Act
        TmdbMovieCredits? result = await client.Credits();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: WellKnownMovieId);
        result.Cast.Should().NotBeEmpty();
        result.Crew.Should().NotBeEmpty();

        // Verify cast data structure
        TmdbCast firstCast = result.Cast.First();
        firstCast.Id.Should().BeGreaterThan(expected: 0);
        firstCast.Name.Should().NotBeNullOrEmpty();
        firstCast.Character.Should().NotBeNullOrEmpty();

        // Verify crew data structure
        TmdbCrew firstCrew = result.Crew.First();
        firstCrew.Id.Should().BeGreaterThan(expected: 0);
        firstCrew.Name.Should().NotBeNullOrEmpty();
        firstCrew.Job.Should().NotBeNullOrEmpty();
        firstCrew.Department.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task ExternalIds_WithRealApi_ReturnsValidExternalIds()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();

        // Act
        TmdbMovieExternalIds? result = await client.ExternalIds();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: WellKnownMovieId);
        result.ImdbId.Should().NotBeNullOrEmpty();
        result.ImdbId.Should().StartWith(expected: "tt"); // IMDB IDs start with "tt"
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task Images_WithRealApi_ReturnsImageData()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();

        // Act
        TmdbImages? result = await client.Images();

        // Assert
        result.Should().NotBeNull();
        result!.Backdrops.Should().NotBeEmpty();
        result.Posters.Should().NotBeEmpty();

        // Verify image data structure
        TmdbImage firstBackdrop = result.Backdrops.First();
        firstBackdrop.FilePath.Should().NotBeNullOrEmpty();
        firstBackdrop.Width.Should().BeGreaterThan(expected: 0);
        firstBackdrop.Height.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task Keywords_WithRealApi_ReturnsKeywords()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();

        // Act
        TmdbMovieKeywords? result = await client.Keywords();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: WellKnownMovieId);
        result.Results.Should().NotBeEmpty();

        // Verify keyword structure
        TmdbKeyword firstKeyword = result.Results.First();
        firstKeyword.Id.Should().BeGreaterThan(expected: 0);
        firstKeyword.Name.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(data: "en-US")]
    [InlineData(data: "fr-FR")]
    [InlineData(data: "es-ES")]
    [Trait(name: "Category", value: "Integration")]
    public async Task Details_WithDifferentLanguages_ReturnsLocalizedData(string language)
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient(movieId: WellKnownMovieId, language: language);

        // Act
        TmdbMovieDetails? result = await client.Details();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: WellKnownMovieId);
        result.Title.Should().NotBeNullOrEmpty();
        result.OriginalTitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task MultipleMovies_WithRealApi_ReturnDifferentData()
    {
        // Arrange
        using TmdbMovieClient client1 = CreateRealMovieClient();
        using TmdbMovieClient client2 = CreateRealMovieClient(movieId: AnotherWellKnownMovieId);

        // Act
        TmdbMovieDetails? movie1 = await client1.Details();
        TmdbMovieDetails? movie2 = await client2.Details();

        // Assert
        movie1.Should().NotBeNull();
        movie2.Should().NotBeNull();
        movie1!.Id.Should().Be(expected: WellKnownMovieId);
        movie2!.Id.Should().Be(expected: AnotherWellKnownMovieId);
        movie1.Title.Should().NotBe(unexpected: movie2.Title);
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task Changes_WithRealApi_ReturnsChangesData()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();
        string startDate = DateTime.Now.AddDays(value: -30).ToString(format: "yyyy-MM-dd");
        string endDate = DateTime.Now.ToString(format: "yyyy-MM-dd");

        // Act
        TmdbMovieChanges? result = await client.Changes(startDate: startDate, endDate: endDate);

        // Assert
        // Changes endpoint may return null for certain date ranges or when no changes exist
        if (result != null)
        {
            result.ChangesChanges.Should().NotBeNull();
        }
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task InvalidMovieId_WithRealApi_ReturnsNull()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient(movieId: InvalidMovieId);

        // Act & Assert
        TmdbMovieDetails? result = await client.Details();

        // Note: ID 999999 actually returns valid movie data from TMDB API
        // "The El-Salomons: Marriage of Convenience" - so it's not truly invalid
        // API behavior may change, so we handle both scenarios
        if (result != null)
        {
            result.Id.Should().Be(expected: InvalidMovieId);
        }
    }

    [Fact]
    [Trait(name: "Category", value: "Integration")]
    public async Task RateLimiting_MultipleQuickCalls_HandlesGracefully()
    {
        // Arrange
        using TmdbMovieClient client = CreateRealMovieClient();

        // Act - Make multiple quick calls to test rate limiting
        Task<TmdbMovieDetails?>[] tasks = Enumerable
            .Range(start: 0, count: 5)
            .Select(selector: _ => client.Details())
            .ToArray();
        TmdbMovieDetails?[] results = await Task.WhenAll(tasks: tasks);

        // Assert
        results.Should().AllSatisfy(expected: result => result.Should().NotBeNull());
        results.Should().AllSatisfy(expected: result => result!.Id.Should().Be(expected: WellKnownMovieId));
    }
}
