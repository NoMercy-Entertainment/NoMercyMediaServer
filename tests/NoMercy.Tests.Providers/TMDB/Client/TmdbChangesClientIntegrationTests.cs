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
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
///     Integration tests for TmdbChangesClient - the global daily change lists used to keep
///     locally held metadata in sync. Hits the real TMDB API. limit:1 keeps each call to a
///     single page so the test stays cheap.
/// </summary>
[Trait(name: "Category", value: "Integration")]
[Collection(name: "TmdbApi")]
public class TmdbChangesClientIntegrationTests : TmdbTestBase
{
    [SkippableFact]
    public async Task MovieChanges_WithRealApi_ReturnsChangedMovieIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbChangesClient client = new();

        // Act
        List<TmdbChangeListItem>? result = await client.MovieChanges(limit: 1);
        Skip.If(
            condition: result is null || result.Count == 0,
            reason: "TMDB changes endpoint returned no data (unavailable or rate-limited)."
        );

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result!.Should().AllSatisfy(expected: change => change.Id.Should().BeGreaterThan(expected: 0));
    }

    [SkippableFact]
    public async Task TvChanges_WithRealApi_ReturnsChangedShowIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbChangesClient client = new();

        // Act
        List<TmdbChangeListItem>? result = await client.TvChanges(limit: 1);
        Skip.If(
            condition: result is null || result.Count == 0,
            reason: "TMDB changes endpoint returned no data (unavailable or rate-limited)."
        );

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result!.Should().AllSatisfy(expected: change => change.Id.Should().BeGreaterThan(expected: 0));
    }

    [SkippableFact]
    public async Task PersonChanges_WithRealApi_ReturnsChangedPersonIds()
    {
        // Arrange
        SetupTmdbAuthentication();
        using TmdbChangesClient client = new();

        // Act
        List<TmdbChangeListItem>? result = await client.PersonChanges(limit: 1);
        Skip.If(
            condition: result is null || result.Count == 0,
            reason: "TMDB changes endpoint returned no data (unavailable or rate-limited)."
        );

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result!.Should().AllSatisfy(expected: change => change.Id.Should().BeGreaterThan(expected: 0));
    }
}
