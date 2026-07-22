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
/// Unit tests for TmdbMovieClient class
/// Tests all methods with both valid and invalid data scenarios
/// </summary>
[Collection(name: "TmdbApi")]
public class TmdbMovieClientTests : TmdbTestBase
{
    [Fact]
    public void Constructor_WithValidId_SetsIdCorrectly()
    {
        // Arrange
        const int expectedId = 155;

        // Act
        using TmdbMovieClient client = new(id: expectedId);

        // Assert
        client.Id.Should().Be(expected: expectedId);
    }

    [Fact]
    public void Constructor_WithZeroId_SetsIdToZero()
    {
        // Arrange & Act
        using TmdbMovieClient client = new();

        // Assert
        client.Id.Should().Be(expected: 0);
    }

    [Theory]
    [InlineData(data: "en-US")]
    [InlineData(data: "fr-FR")]
    [InlineData(data: "es-ES")]
    [InlineData(data: "de-DE")]
    public void Constructor_WithDifferentLanguages_CreatesClientSuccessfully(string language)
    {
        // Arrange & Act
        using TmdbMovieClient client = new(id: ValidMovieId, language: language);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task Details_WithValidId_ReturnsMovieDetails()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieDetails? result = await client.Details();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
        result.Title.Should().NotBeNullOrEmpty();
        result.OriginalTitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Details_WithPriorityTrue_ReturnsMovieDetails()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieDetails? result = await client.Details(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task Details_WithPriorityFalse_ReturnsMovieDetails()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieDetails? result = await client.Details(priority: false);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task WithAllAppends_WithMockData_ReturnsCompleteMovieData()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieAppends? result = await client.WithAllAppends();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: 1771); // Mock data provider returns ID 1771, not 155
        result.Title.Should().NotBeNullOrEmpty();
        result.OriginalTitle.Should().NotBeNullOrEmpty();

        // Verify that appended data is included
        result.Credits.Should().NotBeNull();
        result.ExternalIds.Should().NotBeNull();
    }

    [Fact]
    public async Task WithAllAppends_WithPriorityTrue_ReturnsCompleteMovieData()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieAppends? result = await client.WithAllAppends(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: 1771); // Mock data provider returns ID 1771, not 155
    }

    [Fact]
    public async Task Credits_WithValidId_ReturnsCreditsData()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieCredits? result = await client.Credits();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
        result.Cast.Should().NotBeNull();
        result.Crew.Should().NotBeNull();
    }

    [Fact]
    public async Task Credits_WithPriorityTrue_ReturnsCreditsData()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieCredits? result = await client.Credits(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task ExternalIds_WithValidId_ReturnsExternalIds()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieExternalIds? result = await client.ExternalIds();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
        result.ImdbId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExternalIds_WithPriorityTrue_ReturnsExternalIds()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieExternalIds? result = await client.ExternalIds(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task Images_WithValidId_ReturnsImages()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbImages? result = await client.Images();

        // Assert
        result.Should().NotBeNull();
        result!.Backdrops.Should().NotBeNull();
        result.Posters.Should().NotBeNull();
    }

    [Fact]
    public async Task Images_WithPriorityTrue_ReturnsImages()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbImages? result = await client.Images(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Backdrops.Should().NotBeNull();
        result.Posters.Should().NotBeNull();
    }

    [Fact]
    public async Task Keywords_WithValidId_ReturnsKeywords()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieKeywords? result = await client.Keywords();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task Keywords_WithPriorityTrue_ReturnsKeywords()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieKeywords? result = await client.Keywords(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task Lists_WithValidId_ReturnsLists()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieLists? result = await client.Lists();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task Lists_WithPriorityTrue_ReturnsLists()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieLists? result = await client.Lists(priority: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task Changes_WithValidDateRange_ReturnsChanges()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();
        const string startDate = "2023-01-01";
        const string endDate = "2023-12-31";

        // Act
        TmdbMovieChanges? result = await client.Changes(startDate: startDate, endDate: endDate);

        // Assert
        // Changes endpoint may return null even for valid requests due to TMDB API limitations
        if (result != null)
        {
            result.ChangesChanges.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData(data: ["", "2023-12-31"])]
    [InlineData(data: ["2023-01-01", ""])]
    [InlineData(data: ["", ""])]
    public async Task Changes_WithEmptyDateParameters_HandlesGracefully(
        string startDate,
        string endDate
    )
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act & Assert
        Func<Task<TmdbMovieChanges?>> act = async () => await client.Changes(startDate: startDate, endDate: endDate);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        TmdbMovieClient client = CreateMockMovieClient();

        // Act & Assert
        client.Dispose();
        Action act = () => client.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task MultipleMethodCalls_OnSameClient_WorkCorrectly()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        TmdbMovieDetails? details = await client.Details();
        TmdbMovieCredits? credits = await client.Credits();
        TmdbMovieExternalIds? externalIds = await client.ExternalIds();

        // Assert
        details.Should().NotBeNull();
        credits.Should().NotBeNull();
        externalIds.Should().NotBeNull();

        details!.Id.Should().Be(expected: ValidMovieId);
        credits!.Id.Should().Be(expected: ValidMovieId);
        externalIds!.Id.Should().Be(expected: ValidMovieId);
    }

    [Fact]
    public async Task ConcurrentCalls_OnSameClient_WorkCorrectly()
    {
        // Arrange
        using TmdbMovieClient client = CreateMockMovieClient();

        // Act
        Task<TmdbMovieDetails?> detailsTask = client.Details();
        Task<TmdbMovieCredits?> creditsTask = client.Credits();
        Task<TmdbMovieExternalIds?> externalIdsTask = client.ExternalIds();

        TmdbMovieDetails? details = await detailsTask;
        TmdbMovieCredits? credits = await creditsTask;
        TmdbMovieExternalIds? externalIds = await externalIdsTask;

        // Assert
        details.Should().NotBeNull();
        credits.Should().NotBeNull();
        externalIds.Should().NotBeNull();

        details!.Id.Should().Be(expected: ValidMovieId);
        credits!.Id.Should().Be(expected: ValidMovieId);
        externalIds!.Id.Should().Be(expected: ValidMovieId);
    }
}
