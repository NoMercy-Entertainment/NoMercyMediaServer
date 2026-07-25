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
/// Unit tests for TmdbPersonClient
/// Tests person details, credits, images, and related metadata
/// </summary>
[Trait("Category", "Unit")]
[Collection("TmdbApi")]
public class TmdbPersonClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidId_SetsIdCorrectly()
    {
        // Arrange
        const int expectedId = ValidPersonId;

        // Act
        using TmdbPersonClient client = new(expectedId);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(expectedId);
    }

    #endregion

    // TODO: Implement remaining Person client tests
    // - Details tests
    // - WithAllAppends tests
    // - Credits (movie/tv) tests
    // - Images, ExternalIds tests
    // - Combined credits tests
}
