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

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbEpisodeClient
/// Tests episode details, credits, images, and related metadata
/// </summary>
[Trait("Category", "Unit")]
[Collection("TmdbApi")]
public class TmdbEpisodeClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_SetsValuesCorrectly()
    {
        // Arrange
        const int tvId = ValidTvShowId;
        const int seasonNumber = 1;
        const int episodeNumber = 1;

        // Act
        using TmdbEpisodeClient client = new(tvId, seasonNumber, episodeNumber);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(tvId);
    }

    #endregion

    // TODO: Implement remaining Episode client tests
    // - Details tests
    // - WithAllAppends tests
    // - Credits, Images, Videos tests
    // - ExternalIds, Translations tests
}
