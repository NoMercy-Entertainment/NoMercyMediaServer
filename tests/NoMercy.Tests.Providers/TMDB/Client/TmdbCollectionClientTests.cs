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
/// Unit tests for TmdbCollectionClient
/// Tests movie collection details and related metadata
/// </summary>
[Trait("Category", "Unit")]
[Collection("TmdbApi")]
public class TmdbCollectionClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidId_SetsIdCorrectly()
    {
        // Arrange
        const int expectedId = ValidCollectionId;

        // Act
        using TmdbCollectionClient client = new(expectedId);

        // Assert
        client.Should().NotBeNull();
        client.Id.Should().Be(expectedId);
    }

    #endregion

    // TODO: Implement remaining Collection client tests
    // - Details tests
    // - Images tests
    // - Translations tests
}
