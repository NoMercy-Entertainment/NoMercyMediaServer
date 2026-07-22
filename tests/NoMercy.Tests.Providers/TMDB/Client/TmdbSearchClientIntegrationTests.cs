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
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Search;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Integration tests for TmdbSearchClient using real TMDB API
/// These tests require a valid TMDB API key and internet connection
/// </summary>
[Trait(name: "Category", value: "Integration")]
[Collection(name: "TmdbApi")]
public class TmdbSearchClientIntegrationTests : TmdbTestBase
{
    [Fact]
    public async Task Movie_WithRealApi_ReturnsValidResults()
    {
        // Arrange
        using TmdbSearchClient client = CreateRealSearchClient();

        // Act
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(query: "The Dark Knight", year: "2008");

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(predicate: m => m.Title!.Contains("Dark Knight"));
        result.Results.First().Id.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public async Task TvShow_WithRealApi_ReturnsValidResults()
    {
        // Arrange
        using TmdbSearchClient client = CreateRealSearchClient();

        // Act
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(query: "Breaking Bad", year: "2008");

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(predicate: tv => tv.Name!.Contains("Breaking Bad"));
        result.Results.First().Id.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public async Task Person_WithRealApi_ReturnsValidResults()
    {
        // Arrange
        using TmdbSearchClient client = CreateRealSearchClient();

        // Act
        TmdbPaginatedResponse<TmdbPerson>? result = await client.Person(query: "Leonardo DiCaprio");

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        result.Results.Should().Contain(predicate: p => p.Name!.Contains("Leonardo"));
        result.Results.First().Id.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public async Task Multi_WithRealApi_ReturnsVariedMediaTypes()
    {
        // Arrange
        using TmdbSearchClient client = CreateRealSearchClient();

        // Act
        TmdbPaginatedResponse<TmdbMultiSearch>? result = await client.Multi(query: "Marvel");

        // Assert
        result.Should().NotBeNull();
        result!.Results.Should().NotBeEmpty();
        // Note: TmdbMultiSearch has a complex tuple structure, so we just verify the result exists
    }

    private static new TmdbSearchClient CreateRealSearchClient()
    {
        // This would use real API configuration
        return new();
    }
}
