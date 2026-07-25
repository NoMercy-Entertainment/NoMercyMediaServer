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
/// Unit tests for TmdbConfigClient
/// Tests TMDB configuration, image settings, and available regions
/// </summary>
[Trait("Category", "Unit")]
[Collection("TmdbApi")]
public class TmdbConfigClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithNoParameters_CreatesInstance()
    {
        // Act
        using TmdbConfigClient client = new();

        // Assert
        client.Should().NotBeNull();
    }

    #endregion

    // TODO: Implement remaining Config client tests
    // - Configuration tests
    // - Countries tests
    // - Jobs tests
    // - Languages tests
    // - Primary translations tests
    // - Timezones tests
}
